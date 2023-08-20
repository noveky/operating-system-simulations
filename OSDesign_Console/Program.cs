using OSDesign_Console.Scheduling;
using OSDesign_Console.MemoryManaging;
using OSDesign_Console.FileSystem;

namespace OSDesign_Console
{
	public struct PrintSegment
	{
		public string? Text;
		public ConsoleColor? Color;

		public PrintSegment(string? text, ConsoleColor? color = null)
		{
			Text = text;
			Color = color;
		}
	}

	internal class Program
	{
		public static readonly object consoleLock = new();

		public static void Clear()
		{
			lock (consoleLock)
			{
				//for (int i = 0; i < Console.BufferHeight * 2; ++i) Console.WriteLine();
				Console.Clear();
				Console.WriteLine("\x1b[3J");
				Console.Clear();
			}
		}

		public static void PrintSegments(params PrintSegment[] segments)
		{
			lock (consoleLock)
			{
				foreach (var segment in segments)
				{
					if (segment.Color != null)
					{
						Console.ForegroundColor = (ConsoleColor)segment.Color;
					}
					Console.Write(segment.Text);
					Console.ResetColor();
				}
			}
		}

		public static void PrintLnSegments(params PrintSegment[] segments)
		{
			lock (consoleLock)
			{
				PrintSegments(segments);
				Console.WriteLine();
			}
		}

		public static void Print(string? text = null, ConsoleColor? color = null)
		{
			PrintSegments(new PrintSegment(text, color));
		}

		public static void PrintLn(string? text = null, ConsoleColor? color = null)
		{
			PrintLnSegments(new PrintSegment(text, color));
		}

		public static T Input<T>(string? prompt = null, ConsoleColor? promptColor = null)
		{
			lock (consoleLock)
			{
				T i;
				while (true)
				{
					if (promptColor != null)
					{
						Console.ForegroundColor = (ConsoleColor)promptColor;
					}
					Console.Write(prompt);
					Console.ResetColor();
					try
					{
						i = (T)Convert.ChangeType(Console.ReadLine()!, typeof(T));
						return i;
					}
					catch { }
				}
			}
		}

		public static void Pause(string? prompt = null)
		{
			lock (consoleLock)
			{
				Console.Write(prompt);
				Console.ReadKey();
			}
		}

		static void Main(string[] args)
		{
			while (true)
			{
				Clear();
				Console.Title = "模拟实验";
				PrintLnSegments(new("1. ", ConsoleColor.DarkGray), new("进程调度实验", ConsoleColor.White));
				PrintLnSegments(new("2. ", ConsoleColor.DarkGray), new("虚拟内存管理实验", ConsoleColor.White));
				PrintLnSegments(new("3. ", ConsoleColor.DarkGray), new("文件系统", ConsoleColor.White));
				PrintLnSegments(new("0. ", ConsoleColor.DarkGray), new("退出", ConsoleColor.White));
				PrintLn();
				int option = Input<int>("请输入数字 (0~3)：");
				if (option != 0)
				{
					Clear();
				}
				switch (option)
				{
					case 0:
						return;
					case 1:
						Console.Title = "模拟实验 - 进程调度实验";
						Scheduling.Scheduling.Test();
						break;
					case 2:
						Console.Title = "模拟实验 - 虚拟内存管理实验";
						MemoryManaging.MemoryManaging.Test();
						break;
					case 3:
						Console.Title = "模拟实验 - 文件系统";
						FileSystem.FileSystem.Test();
						break;
					default:
						continue;
				}
				PrintLn();
				Pause("请按任意键继续...");
			}
		}
	}
}