using OSDesign_Console.Scheduling;
using System.Runtime.CompilerServices;

namespace OSDesign_Console
{

	internal class Program
	{
		public static int Time { get; set; } = 0;

		static readonly object consoleLock = new();

		public static void SchedulingLog(string sender, ConsoleColor senderColor, string str)
		{
			lock (consoleLock)
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.Write($"({Time,4} ms) ");
				Console.ForegroundColor = senderColor;
				Console.Write($"[{sender}] ");
				Console.ForegroundColor = ConsoleColor.Gray;
				; ; ;
				Console.WriteLine(str);
				Console.ResetColor();
			}
		}

		static void Main(string[] args)
		{
			Scheduler scheduler = new();

			Operation operation1 = new(OperationType.Computation, 100);
			Operation operation2 = new(OperationType.IO, 200);
			Operation operation3 = new(OperationType.Computation, 300);
			Operation operation4 = new(OperationType.Computation, 400);
			Operation operation5 = new(OperationType.IO, 500);
			Operation operation6 = new(OperationType.Computation, 600);

			scheduler.CreateProcess(operation1, operation2, operation3);
			scheduler.CreateProcess(operation4);
			scheduler.CreateProcess(operation5, operation6);

			scheduler.Run().Wait();
		}
	}
}