using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OSDesign_Console.FileSystem
{
	class IdGenerator
	{
		readonly HashSet<int> ids = new();

		public int NewId() // 生成新 id
		{
			int id = 0;
			for (; ids.Contains(id); ++id) ;
			return NewId(id);
		}

		public int NewId(int id) // 添加新 id
		{
			ids.Add(id);
			return id;
		}

		public void Restore()
		{
			ids.Clear();
		}
	}

	[Serializable]
	class UserInfo
	{
		public int Id;
		public string Name;

		public static readonly IdGenerator idGenerator = new();

		public UserInfo(string name) // 新建（自动生成 id）
		{
			Id = idGenerator.NewId();
			Name = name;
		}

		public override string ToString() => $"{Name}";
	}

	[Serializable]
	class FileInfo
	{
		public int Id;
		public string Name;
		public long StartAddress;
		public long EndAddress;
		public long Length; // 每次写文件的时候，都要更新这个值
		public bool ProtectionFieldR; // 读保护字段
		public bool ProtectionFieldW; // 写保护字段
		public bool ProtectionFieldX; // 执行保护字段
		public int UserId; // 外键

		public static readonly IdGenerator idGenerator = new();

		public FileInfo(string name, long startAddress, bool protectionFieldR, bool protectionFieldW, bool protectionFieldX, int userId) // 新建（自动生成 id，文件为空）
		{
			Id = idGenerator.NewId();
			Name = name;
			StartAddress = startAddress;
			EndAddress = startAddress;
			Length = 0;
			ProtectionFieldR = protectionFieldR;
			ProtectionFieldW = protectionFieldW;
			ProtectionFieldX = protectionFieldX;
			UserId = userId;
		}

		public override string ToString() => $"{{ 文件名：{Name}, 开始地址：0x{StartAddress:X}，结束地址：0x{EndAddress:X}，文件长度：{FileSystem.GetSizeStr(Length)}，保护字段 (R, W, X)：({(ProtectionFieldR ? 1 : 0)}, {(ProtectionFieldW ? 1 : 0)}, {(ProtectionFieldX ? 1 : 0)}) }}";
	}

	[Serializable]
	class OpenFileInfo
	{
		public int Id; // 即文件描述符
		public long ReadPointer; // 读指针
		public int FileId; // 外键

		public static readonly IdGenerator idGenerator = new();

		public OpenFileInfo(long pointer, int fileId) // 打开文件
		{
			Id = idGenerator.NewId();
			ReadPointer = pointer;
			FileId = fileId;
		}

		public override string ToString() => $"{{ 文件描述符：{Id}，读指针：0x{ReadPointer:X} }}";
	}

	[Serializable]
	public class DiskStorage
	{
		public long Size;
		readonly byte[] disk;

		public DiskStorage(long size)
		{
			Size = size;
			disk = new byte[Size];
		}

		public byte[] Read(long address, long bytesToRead)
		{
			bytesToRead = Math.Min(bytesToRead, Size - address);
			byte[] data = new byte[bytesToRead];
			for (long i = 0; i < bytesToRead; ++i)
			{
				data[i] = disk[address + i];
			}
			return data;
		}

		public void Write(long address, byte[] data)
		{
			long bytesToWrite = Math.Min(data.LongLength, Size - address);
			for (long i = 0; i < bytesToWrite; ++i)
			{
				disk[address + i] = data[i];
			}
		}
	}

	[Serializable]
	class FAT
	{
		readonly long[] table; // EOF：文件尾块，0：空（所以 0 号块应该预留），正整数：下一文件盘块

		public long this[long blockNumber]
		{
			get => table[blockNumber];
			set
			{
				table[blockNumber] = value;
			}
		}

		public FAT(long blockCount)
		{
			table = new long[blockCount];
		}

		public string ToString(long blockNumber)
		{
			long t = table[blockNumber];
			return
				t == -1
				? "EOF"
				: t == 0
				? "-"
				: $"{t}";
		}

		public override string ToString()
		{
			string str = "[";
			for (long i = 1; i < table.LongLength; ++i)
			{
				if (i % 10 == 1) str += $"\n  ({i,3} ~ {Math.Min(i + 9, table.LongLength - 1),3})\t";
				str += $" {ToString(i)}\t";
			}
			return str.TrimEnd(',') + "\n]";
		}

		public long GetVacantBlockCount() => table.Sum(t => t == 0 ? 1 : 0) - 1; // 需要去掉预留的第 0 块
	}

	[Serializable]
	class FileOperatingSystem
	{
		readonly long blockSize; // 持久化
		readonly int maxFileCountPerUser = 10; // 持久化
		readonly int maxOpenFileCount = 16; // 持久化

		const long EOF = -1;

		readonly DiskStorage disk; // 持久化

		long BlockCount => disk.Size / blockSize;

		readonly Dictionary<string, UserInfo> users = new(); // 持久化
		readonly Dictionary<int, FileInfo> files = new(); // 持久化
		readonly Dictionary<int, OpenFileInfo> openFiles = new(); // 非持久化，序列化之前需要清空

		readonly FAT fat; // 持久化

		string UsersToString
		{
			get
			{
				string str = "{";
				foreach (UserInfo user in users.Values)
				{
					str += $" {user},";
				}
				return str.TrimEnd(',') + " }";
			}
		}

		public FileOperatingSystem(long diskSize, long blockSize, int maxFileCountPerUser, int maxOpenFileCount)
		{
			if (diskSize / blockSize * blockSize != diskSize)
			{
				throw new ArgumentException("块大小无效");
			}

			this.blockSize = blockSize;
			this.maxFileCountPerUser = maxFileCountPerUser;
			this.maxOpenFileCount = maxOpenFileCount;

			disk = new(diskSize);

			fat = new FAT(BlockCount);
		}

		public static void Log(string? str) => FileSystem.Log($"[文件系统] {str}", ConsoleColor.DarkGray);

		public static FileOperatingSystem Create()
		{
			FileOperatingSystem fs = new(1L * 256L, 4L * 1L, 10, 16);
			return fs;
		}

		public static FileOperatingSystem CreateForTest()
		{
			FileOperatingSystem fs = Create();

			// 添加初始用户
			fs.CreateUser("user1");
			fs.CreateUser("user2");
			fs.CreateUser("user3");
			fs.CreateUser("user4");
			fs.CreateUser("user5");
			fs.CreateUser("user6");
			fs.CreateUser("user7");
			fs.CreateUser("user8");

			// 添加初始文件
			FileInfo file11 = fs.CreateFile(fs.GetUser("user1"), "file1", true, true, false);
			FileInfo file21 = fs.CreateFile(fs.GetUser("user2"), "file1", true, true, true);
			FileInfo file12 = fs.CreateFile(fs.GetUser("user1"), "file2", true, true, true);
			fs.CreateFile(fs.GetUser("user1"), "file3", false, false, true); // file13
			fs.CreateFile(fs.GetUser("user1"), "file3", false, false, true); // file13；测试重复创建文件
			FileInfo file14 = fs.CreateFile(fs.GetUser("user1"), "file4", true, true, true);
			OpenFileInfo open11 = fs.OpenFile(file11);
			fs.WriteFile(open11, FileSystem.TextToData("文字TEXTtext123"));
			fs.CloseFile(open11);
			OpenFileInfo open12 = fs.OpenFile(file12);
			fs.WriteFile(open12, FileSystem.TextToData("anotherTEXT"));
			fs.CloseFile(open12);
			OpenFileInfo open21 = fs.OpenFile(file21);
			fs.WriteFile(open21, FileSystem.TextToData("yetANOTHERtext"));
			fs.CloseFile(open21);
			OpenFileInfo open14 = fs.OpenFile(file14);
			fs.WriteFile(open14, FileSystem.TextToData("LongText1234567890987654321!@#$%^&*()abcdefghijk"));
			fs.CloseFile(open14);

			fs.openFiles.Clear();

			return fs;
		}

		public override string ToString() => $"{{ 磁盘剩余空间：{FileSystem.GetSizeStr(GetStorageRemaining())} / {FileSystem.GetSizeStr(disk.Size)}，分块大小：{FileSystem.GetSizeStr(blockSize)}，每个用户最大文件数：{maxFileCountPerUser}，最大打开文件数：{maxOpenFileCount}，用户数：{users.Count}，文件数：{files.Count}，当前打开文件数：{openFiles.Count}，用户：{UsersToString}，文件分配表：{fat} }}";

		const string SerializedFilePath = "filesys.bin";

		public void Close()
		{
			// 将非持久化数据清空
			openFiles.Clear();

			// 序列化并写入文件
			byte[] data = ToBinary();
			using FileStream stream = new(SerializedFilePath, FileMode.Create, FileAccess.Write);
			stream.Write(data);

			FileSystem.PrintData("将文件系统序列化为二进制数据：", data);
		}

		public static FileOperatingSystem LoadOrCreateForTest()
		{
			try
			{
				byte[] data;
				using (FileStream stream = new(SerializedFilePath, FileMode.Open, FileAccess.Read))
				{
					data = new byte[stream.Length];
					stream.Read(data);
				}

				FileSystem.PrintData("将二进制数据反序列化为文件系统：", data);

				FileOperatingSystem fs = FromBinary(data);
				fs.users.Values.ToList().ForEach(t => UserInfo.idGenerator.NewId(t.Id));
				fs.files.Values.ToList().ForEach(t => FileInfo.idGenerator.NewId(t.Id));
				return fs;
			}
			catch
			{
				FileSystem.LogError(new Exception("反序列化失败，将重新创建文件系统"));
				return CreateForTest();
			}
		}

#pragma warning disable SYSLIB0011
		public byte[] ToBinary()
		{
			BinaryFormatter formatter = new();
			using MemoryStream stream = new();
			formatter.Serialize(stream, this);
			return stream.ToArray();
		}

		public static FileOperatingSystem FromBinary(byte[] data)
		{
			BinaryFormatter formatter = new();
			FileOperatingSystem fs;
			using MemoryStream stream = new(data);
			fs = (FileOperatingSystem)formatter.Deserialize(stream);
			return fs;
		}
#pragma warning restore SYSLIB0011

		long GetBlockNumber(long address) => address / blockSize;

		long GetBlockAddress(long blockNumber) => blockNumber * blockSize;

		long GetBlockOffset(long address) => address - GetBlockAddress(GetBlockNumber(address)); // 块内偏移

		byte[] ReadBlock(long blockNumber) => disk.Read(GetBlockAddress(blockNumber), blockSize);

		void WriteBlock(long blockNumber, byte[] data) => disk.Write(GetBlockAddress(blockNumber), data);

		static void CopyData(byte[] from, byte[] to, long start1, long start2, long length)
		{
			while (length-- > 0) { to[start2++] = from[start1++]; }
		}

		public UserInfo CreateUser(string userName)
		{
			// 检查重名
			if (users.ContainsKey(userName)) throw new Exception($"用户名 {userName} 已经存在");

			// 创建用户
			UserInfo user = new(userName);
			users.Add(userName, user);

			return user;
		}

		public void DeleteUser(UserInfo user)
		{
			// 删除所有文件
			List<FileInfo> userFiles = ListUserFiles(user);
			userFiles.ForEach(DeleteFile);

			// 移除用户
			users.Remove(user.Name);
		}

		long AllocateVacantBlock()
		{
			if (fat.GetVacantBlockCount() == 0) throw new Exception("无空闲盘块可分配");

			Random random = new();
			long blockNumber = 0;
			while (blockNumber == 0 || fat[blockNumber] != 0) // 0 是预留块号，不能分配
			{
				blockNumber = random.NextInt64(BlockCount);
			}
			return blockNumber;
		}

		long GetStorageRemaining() => fat.GetVacantBlockCount() * blockSize; // 将所有空块大小求和

		UserInfo GetUser(string userName)
		{
			if (!users.ContainsKey(userName)) throw new Exception($"用户 {userName} 不存在");
			return users[userName];
		}

		UserInfo GetUser(int userId)
		{
			foreach (UserInfo user in users.Values)
			{
				if (user.Id == userId) return user;
			}
			throw new Exception($"ID 为 {userId} 的用户不存在");
		}

		FileInfo GetFile(UserInfo user, string fileName)
		{
			FileInfo? file = TryGetFile(user, fileName);
			if (file != null) return file;
			throw new Exception($"文件 \"{GetFilePath(user, fileName)}\" 不存在");
		}

		FileInfo? TryGetFile(UserInfo user, string fileName)
		{
			foreach (FileInfo file in files.Values)
			{
				if (file.UserId == user.Id && file.Name == fileName) return file;
			}
			return null;
		}

		FileInfo GetFile(int fileId)
		{
			if (!files.ContainsKey(fileId)) throw new Exception($"ID 为 {fileId} 的文件不存在");
			return files[fileId];
		}

		OpenFileInfo GetOpenFile(int fileDescriptor)
		{
			if (!openFiles.ContainsKey(fileDescriptor)) throw new Exception($"文件描述符 {fileDescriptor} 不存在");
			return openFiles[fileDescriptor];
		}

		OpenFileInfo? GetOpenFile(FileInfo file)
		{
			foreach (OpenFileInfo openFile in openFiles.Values)
			{
				if (openFile.FileId == file.Id) return openFile;
			}
			return null;
		}

		static string GetFilePath(UserInfo user, string fileName) => $"{user.Name}/{fileName}";

		string GetFilePath(FileInfo file) => GetFilePath(GetUser(file.UserId), file.Name);

		void RestoreReadPointer(OpenFileInfo openFile)
		{
			FileInfo file = GetFile(openFile.FileId);
			openFile.ReadPointer = file.StartAddress;

			Log($"重置了文件描述符 {openFile.Id} 的读指针，当前为 0x{openFile.ReadPointer:X}");
		}

		List<FileInfo> ListUserFiles(UserInfo user)
		{
			List<FileInfo> userFiles = new();
			foreach (FileInfo file in files.Values)
			{
				if (file.UserId == user.Id) userFiles.Add(file);
			}
			return userFiles;
		}

		OpenFileInfo OpenFile(FileInfo file)
		{
			OpenFileInfo? openFile = GetOpenFile(file);
			if (openFile != null) throw new Exception($"文件 \"{GetFilePath(file)}\" 已经打开：{openFile}");

			if (openFiles.Count >= maxOpenFileCount) throw new Exception("打开文件已达最大数目");
			openFile = new(file.StartAddress, file.Id);
			openFiles.Add(openFile.Id, openFile);
			return openFile;
		}

		void CloseFile(OpenFileInfo openFile)
		{
			openFiles.Remove(openFile.Id);
		}

		FileInfo CreateFile(UserInfo user, string fileName, bool protectionFieldR, bool protectionFieldW, bool protectionFieldX)
		{
			FileInfo? file = TryGetFile(user, fileName);

			// 若文件已存在，则删除
			if (file != null)
			{
				DeleteFile(file);
			}

			// 检查文件数目
			if (ListUserFiles(user).Count >= maxFileCountPerUser) throw new Exception($"用户 {user.Name} 文件已达最大数目");

			// 新建目录项
			long startBlockNumber = AllocateVacantBlock();
			long startAddress = GetBlockAddress(startBlockNumber);
			file = new(fileName, startAddress, protectionFieldR, protectionFieldW, protectionFieldX, user.Id);
			files.Add(file.Id, file);

			// 更新文件分配表
			fat[GetBlockNumber(file.StartAddress)] = -1;

			return file;
		}

		void DeleteFile(FileInfo file)
		{
			// 若文件已打开，则关闭
			OpenFileInfo? openFile = GetOpenFile(file);
			if (openFile != null)
			{
				CloseFile(openFile);
			}

			// 释放盘块
			long blockNumber = GetBlockNumber(file.StartAddress);
			while (blockNumber != EOF)
			{
				if (blockNumber == 0) throw new Exception("文件分配表损坏");

				long newBlockNumber = fat[blockNumber];
				fat[blockNumber] = 0;
				blockNumber = newBlockNumber;
			}

			// 移除目录项
			files.Remove(file.Id);
		}

		byte[] ReadFile(OpenFileInfo openFile, long bytesToRead)
		{
			FileInfo file = GetFile(openFile.FileId);
			if (!file.ProtectionFieldR) throw new Exception($"文件 \"{GetFilePath(file)}\" 无读取权限");

			Log($"开始读取文件 \"{GetFilePath(file)}\"，当前读指针 0x{openFile.ReadPointer:X}");

			byte[] data = new byte[bytesToRead];
			long bytesRead = 0; // 已读取的字节数

			long blockNumber = GetBlockNumber(openFile.ReadPointer); // 当前块号
			long blockOffset = GetBlockOffset(openFile.ReadPointer); // 当前块中开始读取地址的块内偏移
			while (blockNumber != EOF && bytesToRead > 0) // 文件未结束，且还需要继续读取
			{
				if (blockNumber == 0) throw new Exception("文件分配表出现意外的零项");

				// 读当前块
				byte[] block = ReadBlock(blockNumber);

				long nextBlockNumber = fat[blockNumber];

				long blockBytesToRead; // 当前块中需要读取的字节数

				// 首先算出当前块中最多可以读到多少字节
				if (nextBlockNumber == EOF)
				{
					blockBytesToRead = GetBlockOffset(file.EndAddress) - blockOffset;
				}
				else
				{
					blockBytesToRead = blockSize - blockOffset;
				}

				// 然后根据需要，取需要读取的字节数
				blockBytesToRead = Math.Min(blockBytesToRead, bytesToRead);

				CopyData(block, data, blockOffset, bytesRead, blockBytesToRead);

				byte[] blockDataRead = new byte[blockBytesToRead]; // 当前块中读取的内容
				Array.Copy(block, blockOffset, blockDataRead, 0, blockBytesToRead);

				Log($"当前盘块号：{blockNumber,3}，下一盘块号：{nextBlockNumber,3}，读取内容：{FileSystem.BinaryToString(blockDataRead)}(H) \"{FileSystem.DataToText(blockDataRead)}\"");

				bytesRead += blockBytesToRead;
				bytesToRead -= blockBytesToRead;
				openFile.ReadPointer = GetBlockAddress(blockNumber) + blockOffset + blockBytesToRead; // 移动读指针
				blockOffset = 0; // 下一块一定是从块的起始处开始读

				blockNumber = nextBlockNumber;
			}

			if (bytesToRead > 0) throw new Exception("读取超过文件尾");

			Log($"读取结束，当前读指针 0x{openFile.ReadPointer:X}");

			return data;
		}

		void WriteFile(OpenFileInfo openFile, byte[] data, long bytesToWrite = -1)
		{
			FileInfo file = GetFile(openFile.FileId);
			if (!file.ProtectionFieldW) throw new Exception($"文件 \"{GetFilePath(file)}\" 无写入权限");

			if (bytesToWrite == -1) bytesToWrite = data.LongLength;
			else if (bytesToWrite < 0) throw new Exception("写入字节数无效");

			if (bytesToWrite > data.LongLength) throw new Exception("写入字节数超过写入数据长度");

			if (bytesToWrite > GetStorageRemaining() + blockSize - GetBlockOffset(file.EndAddress)) throw new Exception("磁盘剩余空间不足"); // 空块求和，加上文件尾的一部分块大小，就是该文件写入时可用的总剩余空间

			Log($"开始写入文件 \"{GetFilePath(file)}\"");

			long bytesWritten = 0; // 已写入的字节数

			long blockNumber = GetBlockNumber(file.EndAddress); // 当前块号
			long blockOffset = GetBlockOffset(file.EndAddress); // 当前块中开始写入地址的块内偏移
			bool eof = false;
			while (!eof) // 还需要继续写入
			{
				// 先把当前块读过来
				byte[] block = ReadBlock(blockNumber);

				long blockBytesToWrite; // 当前块中需要写入的字节数

				long nextBlockNumber;

				if (bytesToWrite >= blockSize) // 当前块写不完（未写字节数正好等于块长度也属于这个情况，需要新开一个空块，再把空块指向EOF）
				{
					blockBytesToWrite = blockSize - blockOffset;
					fat[blockNumber] = EOF; // 先占用当前盘块
					nextBlockNumber = AllocateVacantBlock();
				}
				else // 当前块能写完
				{
					eof = true;
					blockBytesToWrite = bytesToWrite;
					nextBlockNumber = EOF;
				}

				// 在文件分配表中记录下一盘块号
				fat[blockNumber] = nextBlockNumber;

				// 把数据写到块中
				CopyData(data, block, bytesWritten, blockOffset, blockBytesToWrite);

				byte[] blockDataWritten = new byte[blockBytesToWrite]; // 当前块中写入的内容
				Array.Copy(block, blockOffset, blockDataWritten, 0, blockBytesToWrite);

				Log($"当前盘块号：{blockNumber,3}，下一盘块号：{nextBlockNumber,3}，写入内容：{FileSystem.BinaryToString(blockDataWritten)}(H) \"{FileSystem.DataToText(blockDataWritten)}\"");

				// 把新的块写回去
				WriteBlock(blockNumber, block);

				bytesWritten += blockBytesToWrite;
				bytesToWrite -= blockBytesToWrite;
				file.EndAddress = GetBlockAddress(blockNumber) + blockOffset + blockBytesToWrite; // 移动文件尾
				blockOffset = 0; // 下一块一定是从块的起始处开始写

				blockNumber = nextBlockNumber;
			}

			// 更新文件长度
			file.Length += bytesWritten;

			Log($"写入结束，当前文件长度 0x{file.Length}");
		}

		#region Commands

		static void BreakFilePath(string filePath, out string userName, out string fileName)
		{
			string[] split = filePath.Trim().Split('/');
			if (split.Length != 2) throw new Exception($"路径 \"{filePath}\" 无效");
			userName = split[0];
			fileName = split[1];
		}

		static void Output(string? text)
		{
			FileSystem.Log(text, ConsoleColor.White);
		}

		void CmdDir(string userName)
		{
			UserInfo user = GetUser(userName);
			List<FileInfo> userFiles = ListUserFiles(user);
			Output("[");
			userFiles.ForEach(file => Output($"{file},"));
			Output("]");
		}

		void CmdOpen(string filePath)
		{
			BreakFilePath(filePath, out string userName, out string fileName);
			UserInfo user = GetUser(userName);
			FileInfo file = GetFile(user, fileName);
			OpenFileInfo openFile = OpenFile(file);
			Output($"文件 \"{filePath}\" 已打开：{openFile}");
		}

		void CmdClose(int fileDescriptor)
		{
			OpenFileInfo openFile = GetOpenFile(fileDescriptor);
			CloseFile(openFile);
			Output($"文件描述符 {fileDescriptor} 已关闭");
		}

		void CmdCreate(string filePath, string protectionFields)
		{
			if (protectionFields.Length != 3 || protectionFields.Replace("0", "").Replace("1", "") != "") throw new Exception("保护字段参数无效");
			bool protectionFieldR = int.Parse(protectionFields[0..1]) == 1;
			bool protectionFieldW = int.Parse(protectionFields[1..2]) == 1;
			bool protectionFieldX = int.Parse(protectionFields[2..3]) == 1;
			BreakFilePath(filePath, out string userName, out string fileName);
			UserInfo user = GetUser(userName);
			FileInfo file = CreateFile(user, fileName, protectionFieldR, protectionFieldW, protectionFieldX);
			OpenFileInfo openFile = OpenFile(file);
			Output($"文件 \"{filePath}\" 已创建：{file}");
			Output($"新文件已打开：{openFile}");
		}

		void CmdDelete(string filePath)
		{
			BreakFilePath(filePath, out string userName, out string fileName);
			UserInfo user = GetUser(userName);
			FileInfo file = GetFile(user, fileName);
			DeleteFile(file);
			Output($"文件 \"{filePath}\" 已删除");
		}

		void CmdRead(int fileDescriptor, long? bytesToRead = null)
		{
			OpenFileInfo openFile = GetOpenFile(fileDescriptor);
			FileInfo file = GetFile(openFile.FileId);
			RestoreReadPointer(openFile);
			byte[] data = ReadFile(openFile, bytesToRead ?? file.Length);
			string text = FileSystem.DataToText(data);
			Output($"{text}");
		}

		void CmdWrite(int fileDescriptor, string text)
		{
			OpenFileInfo openFile = GetOpenFile(fileDescriptor);
			byte[] data = FileSystem.TextToData(text);
			WriteFile(openFile, data, data.LongLength);
			Output($"写入完成，当前文件信息：{GetFile(openFile.FileId)}");
		}

		static void CmdGuide()
		{
			FileSystem.Log(
				"""
				命令说明：
				?					显示命令说明
				exit					退出并保存文件系统
				restore					重置文件系统
				info					显示文件系统信息
				dir <用户名>				列文件目录
				open <用户名>/<文件名>			打开文件
				close <文件描述符>			关闭文件
				create <用户名>/<文件名> <保护字段>	创建文件
				delete <用户名>/<文件名>		删除文件
				read <文件描述符>			读文件（从文件头开始到文件尾）
				read <文件描述符> <字符数>		读文件（从当前读指针开始指定字符数）
				write <文件描述符> <内容>		写文件
				""",
				ConsoleColor.White
			);
		}

		public static void HandleInput(FileOperatingSystem fs, string input)
		{
			string cmd;
			string args;
			if (!input.Contains(' '))
			{
				cmd = input;
				args = "";
			}
			else
			{
				int ind = input.IndexOf(' ');
				cmd = input[0..ind].Trim();
				args = input[ind..].Trim();
			}

			switch (cmd)
			{
				case "?":
					CmdGuide();
					break;
				case "exit":
					{
						fs.Close();
						FileSystem.Exited = true;

						Log("文件系统已关闭并保存");
					}
					break;
				case "restore":
					{
						fs = FileOperatingSystem.CreateForTest();
						UserInfo.idGenerator.Restore();
						FileInfo.idGenerator.Restore();
						OpenFileInfo.idGenerator.Restore();

						Log($"文件系统已创建并初始化：{fs}");
					}
					break;
				case "info":
					Log($"文件系统信息：{fs}");
					break;
				case "dir":
					fs.CmdDir(args);
					break;
				case "open":
					fs.CmdOpen(args);
					break;
				case "close":
					fs.CmdClose(int.Parse(args));
					break;
				case "create":
					{
						string filePath = args[0..(args.Length - 3)].Trim();
						string protectionFields = args[(args.Length - 3)..].Trim();
						fs.CmdCreate(filePath, protectionFields);
					}
					break;
				case "delete":
					fs.CmdDelete(args);
					break;
				case "read":
					{
						int fileDescriptor;
						string[] split = args.Split(' ');
						if (split.Length == 1)
						{
							fileDescriptor = int.Parse(args);
							fs.CmdRead(fileDescriptor);
						}
						else if (split.Length == 2)
						{
							fileDescriptor = int.Parse(split[0].Trim());
							long bytesToRead = long.Parse(split[1].Trim());
							fs.CmdRead(fileDescriptor, bytesToRead);
						}
						else throw new Exception("参数无效");
					}
					break;
				case "write":
					{
						int ind = args.IndexOf(' ');
						int fileDescriptor = int.Parse(args[0..ind].Trim());
						string text = args[ind..].Trim();
						fs.CmdWrite(fileDescriptor, text);
					}
					break;
				default:
					throw new Exception("命令无效");
			}
		}

		#endregion
	}

	public static class FileSystem
	{
		public static string GetSizeStr(long size)
		{
			string[] units = { "B", "KB", "MB", "GB", "TB" };
			int unitIndex = 0;
			double fileSize = size;

			while (fileSize >= 1024 && unitIndex < units.Length - 1)
			{
				fileSize /= 1024;
				unitIndex++;
			}

			return fileSize >= 100 ? $"{fileSize:0}{units[unitIndex]}" : $"{fileSize:G3}{units[unitIndex]}"; // 在整数部分少于3位时，只保留3位有效数字
		}

		public static string DataToText(byte[] data) => Encoding.Unicode.GetString(data);
		public static byte[] TextToData(string text) => Encoding.Unicode.GetBytes(text);

		public static string BinaryToString(byte[] data)
		{
			StringBuilder stb = new();
			foreach (byte b in data)
			{
				stb.Append($"{b:X2} ");
			}
			return stb.ToString();
		}

		public static void PrintData(string prompt, byte[] data)
		{
			Log(prompt);
			Log(BinaryToString(data), ConsoleColor.DarkGray);
			Log();
		}

		public static void Log(string? str = null, ConsoleColor color = ConsoleColor.Gray) { Program.PrintLn(str, color); }

		public static void LogError(Exception ex) { Log($"错误：{ex.Message}", ConsoleColor.Red); }

		public static T Input<T>(string? prompt = null) { return Program.Input<T>(prompt, ConsoleColor.Gray); }

		static bool Try(Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				LogError(ex);
				return false;
			}
			finally
			{
				Console.WriteLine();
			}
		}

		public static bool Exited { get; set; } = false;

		public static void Test()
		{
			Exited = false;

			FileOperatingSystem fs = FileOperatingSystem.LoadOrCreateForTest();

			Log($"文件系统已加载或新建：{fs}");
			Log();
			FileOperatingSystem.HandleInput(fs, "?");
			Log();

			while (!Exited)
			{
				string input = Input<string>("> ").Trim();
				// 切字符串，处理命令
				Try(() => FileOperatingSystem.HandleInput(fs, input));
			}
		}
	}
}
