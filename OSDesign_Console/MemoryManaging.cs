using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OSDesign_Console.MemoryManaging
{
	class PageTableEntry
	{
		public bool Valid = false;
		public long PhysicalFrameNumber;
	}

	static class DiskStorage
	{
		public static byte[] Read(long address, long bytesToRead)
		{
			byte[] data = new byte[bytesToRead];
			new Random((int)(address & 0xFFFFFFFF - 0x80000000)).NextBytes(data);
			return data;
		}
	}

	class PhysicalMemory
	{
		public long FrameSize;
		public readonly long Size;
		readonly byte[] memory;
		readonly bool[] allocBitmap; // 位示图

		public PhysicalMemory(long size, long frameSize)
		{
			Size = size;
			FrameSize = frameSize;
			memory = new byte[size];
			allocBitmap = new bool[size / frameSize];
		}

		public void AllocateFrame(long frameNumber)
		{
			allocBitmap[frameNumber] = true;
		}

		public void FreeFrame(long frameNumber)
		{
			allocBitmap[frameNumber] = false;
		}

		public byte[] Read(long address, long bytesToRead)
		{
			bytesToRead = Math.Min(bytesToRead, Size - address);
			byte[] data = new byte[bytesToRead];
			for (long i = 0; i < bytesToRead; ++i)
			{
				data[i] = memory[address + i];
			}
			return data;
		}

		public void Write(long address, byte[] data)
		{
			long bytesToWrite = Math.Min(data.LongLength, Size - address);
			for (long i = 0; i < bytesToWrite; ++i)
			{
				memory[address + i] = data[i];
			}
		}

		public long GetFirstVacantFrameNumber()
		{
			for (long fn = 0; fn < allocBitmap.LongLength; ++fn)
			{
				if (!allocBitmap[fn])
				{
					return fn;
				}
			}
			return -1;
		}
	}

	class VirtualMemory
	{
		readonly long pageSize;
		readonly long physicalMemorySize;
		readonly long logicalMemorySize;
		readonly PhysicalMemory physicalMemory;

		readonly PageTableEntry[] pageTable;

		readonly bool[] referenceBits; // clock 算法引用位数组
		long clockPointer = 0; // clock 算法当前页框号

		public VirtualMemory(long physicalMemorySize, long logicalMemorySize, long pageSize)
		{
			if (logicalMemorySize / pageSize * pageSize != logicalMemorySize)
			{
				throw new Exception("页面大小无效");
			}

			this.physicalMemorySize = physicalMemorySize;
			this.logicalMemorySize = logicalMemorySize;
			this.pageSize = pageSize;

			pageTable = new PageTableEntry[logicalMemorySize / pageSize];
			for (long i = 0; i < pageTable.LongLength; ++i)
			{
				pageTable[i] = new();
			}

			physicalMemory = new(physicalMemorySize, pageSize);

			referenceBits = new bool[physicalMemorySize / pageSize];
		}

		public byte Read(long logicalAddress)
		{
			MemoryManaging.Log($"开始对逻辑地址 0x{logicalAddress:X8} 进行地址变换");

			// 地址变换
			long logicalPageNumber = logicalAddress / pageSize;
			long pageOffset = logicalAddress % pageSize;
			long physicalFrameNumber = GetPhysicalFrameNumber(logicalPageNumber);
			long physicalAddress = physicalFrameNumber * pageSize + pageOffset;

			MemoryManaging.Log($"逻辑地址 0x{logicalAddress:X8} 对应物理地址：0x{physicalAddress:X8}");

			// 引用位置 1
			referenceBits[physicalFrameNumber] = true;

			// 读取
			byte data = physicalMemory.Read(physicalAddress, 1)[0];

			MemoryManaging.Log($"读取到逻辑地址 0x{logicalAddress:X8} 的数据：0x{data:X2}");

			return data;
		}
		
		long SelectPageToEvict() // 用 clock 算法
		{
			// 第一轮扫描
			for (long cnt = 0; cnt < physicalMemorySize / pageSize; ++cnt)
			{
				if (referenceBits[clockPointer]) referenceBits[clockPointer] = false;
				else return clockPointer;
				++clockPointer;
			}

			// 若退出 for 循环仍未返回，说明所有引用位都为 1
			// 将所有引用位都置为 0，并返回当前页框号
			Array.Clear(referenceBits);
			return clockPointer;
		}

		long GetPhysicalFrameNumber(long logicalPageNumber)
		{
			if (logicalPageNumber < 0 || logicalPageNumber >= pageTable.LongLength)
			{
				throw new Exception("页表访问越界");
			}
			PageTableEntry entry = pageTable[logicalPageNumber];
			if (entry.Valid)
			{
				// 页表命中

				MemoryManaging.Log($"逻辑页 0x{logicalPageNumber:X} 命中，物理块号：0x{entry.PhysicalFrameNumber:X}");

				return entry.PhysicalFrameNumber;
			}
			else
			{
				// 缺页处理

				MemoryManaging.Log($"逻辑页 0x{logicalPageNumber:X} 缺页");

				long physicalFrameNumber = physicalMemory.GetFirstVacantFrameNumber();
				if (physicalFrameNumber == -1) // 内存已满
				{
					// 淘汰页面
					long evictedLogicalPageNumber = SelectPageToEvict();
					PageTableEntry evictedEntry = pageTable[evictedLogicalPageNumber];
					physicalFrameNumber = evictedEntry.PhysicalFrameNumber;
					physicalMemory.FreeFrame(physicalFrameNumber); // 回收内存
					evictedEntry.Valid = false; // 将页表项置为无效

					MemoryManaging.Log($"逻辑页 0x{evictedLogicalPageNumber:X} 被淘汰，释放物理块 0x{physicalFrameNumber:X}");
				}

				// 从外存调页
				physicalMemory.AllocateFrame(physicalFrameNumber); // 分配内存
				physicalMemory.Write(physicalFrameNumber * physicalMemory.FrameSize, DiskStorage.Read(logicalPageNumber * pageSize, pageSize));

				// 装入页表
				entry.PhysicalFrameNumber = physicalFrameNumber;
				entry.Valid = true;

				MemoryManaging.Log($"逻辑页 0x{logicalPageNumber:X} 调入物理内存，物理块号：0x{physicalFrameNumber:X}");

				return physicalFrameNumber;
			}
		}
	}

	public static class MemoryManaging
	{
		public static void Log(string? str = null, ConsoleColor color = ConsoleColor.White)
		{
			Program.PrintLn(str, color);
		}

		public static T Input<T>(string? prompt = null)
		{
			return Program.Input<T>(prompt, ConsoleColor.Gray);
		}

		static bool Try(Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				Log($"错误：{ex.Message}", ConsoleColor.Red);
				return false;
			}
			finally
			{
				Log();
			}
		}

		public static void Test()
		{
			VirtualMemory? virtualMemory = null;
			while (!Try(() =>
			{
				long pageSizeInKB = Input<long>("请输入页面大小（单位：KB）：");
				virtualMemory = new(256L * 1048576L, 4L * 1073741824L, pageSizeInKB * 1024L);
			})) ;

			long[] accessSequence = { 0x00006123, 0x000070FF, 0x00006A05, 0x1FFFFFFFF, 0x12346700, 0x7FFFF123, 0x7FFFF124, 0x00006123 };

			foreach (long address in accessSequence)
			{
				Log($"访问逻辑地址：0x{address:X8}", ConsoleColor.Cyan);
				Try(() => virtualMemory!.Read(address));
			}
		}
	}
}
