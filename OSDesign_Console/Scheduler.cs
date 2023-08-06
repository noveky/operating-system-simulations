using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSDesign_Console.Scheduling
{
	public enum ProcessState
	{
		Running,
		LowPriorityReady,
		HighPriorityReady,
		IOBlocked,
		Completed,
	}

	public enum IOState
	{
		Idle,
		Requesting,
	}

	public enum OperationType
	{
		Computation,
		IO,
	}

	public class Operation
	{
		public OperationType Type { get; set; }
		public int TimeRemaining { get; set; }

		public Operation(OperationType type, int duration)
		{
			Type = type;
			TimeRemaining = duration;
		}

		public override string ToString() => $"({Type}, {TimeRemaining} ms)";
	}

	public class Process
	{
		static int _nextId = 1;
		static int NextId => _nextId++;

		public int Id { get; }
		public ProcessState State { get; set; }
		public int TimeSlice { get; set; }
		public Queue<Operation> OperationQueue { get; } = new(); // 队首就是当前操作
		public IOState IOState { get; set; }

		Operation? CurrentOperation => OperationQueue.Count == 0 ? null : OperationQueue.Peek();

		public int TimeRemaining => OperationQueue.Sum(o => o.TimeRemaining);
		public string OperationsStr
		{
			get
			{
				string s = "[";
				lock (OperationQueue)
				{
					foreach (var o in OperationQueue)
					{
						s += $" {o},";
					}
				}
				return s.TrimEnd(',') + " ]";
			}
		}

		public Process()
		{
			Id = NextId;
		}

		public override string ToString() => $"{{ Id: {Id}, State: {State}, TimeSlice: {TimeSlice} ms, Operations: {OperationsStr} }}";

		void Log(string str)
		{
			Program.SchedulingLog($"进程 {Id}", ConsoleColor.Yellow, str);
		}

		public async Task Run()
		{
			while (TimeSlice > 0)
			{
				// 当前操作（队首）为空，则进程结束
				if (CurrentOperation == null) return;

				// 当前操作为 I/O 操作，则开始等待 I/O，并归还 CPU 使用权
				if (CurrentOperation.Type == OperationType.IO)
				{
					RequestIO();
					return;
				}

				// 当前操作为计算操作
				Log($"开始执行计算：{CurrentOperation}");
				int runtime = Math.Min(CurrentOperation.TimeRemaining, TimeSlice);
				int time = Program.Time;
				await Task.Delay(runtime); // 模拟计算操作
				Program.Time = time + runtime;
				CurrentOperation.TimeRemaining -= runtime;
				TimeSlice -= runtime;
				if (CurrentOperation.TimeRemaining <= 0)
				{
					Log($"完成计算：{CurrentOperation}");
					OperationQueue.Dequeue();
				}
				else
				{
					Log($"计算中止：{CurrentOperation}");
				}
			}
		}

		async void RequestIO()
		{
			Log($"开始等待 I/O：{CurrentOperation}");
			IOState = IOState.Requesting;
			int delayTime = CurrentOperation!.TimeRemaining;
			int time = Program.Time;
			await Task.Delay(delayTime);
			Program.Time = time + delayTime;
			CurrentOperation.TimeRemaining = 0;
			Log($"完成 I/O：{CurrentOperation}");
			IOState = IOState.Idle;
			OperationQueue.Dequeue();
		}
	}

	public class Scheduler
	{
		readonly Queue<Process> lowPriorityQueue = new(), highPriorityQueue = new(), ioBlockedQueue = new();

		public void Log(string str)
		{
			Program.SchedulingLog("调度器", ConsoleColor.Blue, str);
		}
		
		public void CreateProcess(IEnumerable<Operation> operations)
		{
			Process process = new()
			{
				State = ProcessState.LowPriorityReady,
				TimeSlice = 0,
			};
			operations.ToList().ForEach(process.OperationQueue.Enqueue);
			lowPriorityQueue.Enqueue(process);
			Log($"进程已创建：{process}");
		}

		public void CreateProcess(params Operation[] operations)
		{
			CreateProcess(operations.ToList());
		}
		
		public async Task Run()
		{
			Log("开始调度");
			while (ioBlockedQueue.Count != 0 || highPriorityQueue.Count != 0 || lowPriorityQueue.Count != 0)
			{
				await Schedule();
			}
			Log("完成调度");
		}

		async Task Schedule()
		{
			Process? process;

			if (ioBlockedQueue.Count != 0)
			{
				if (ioBlockedQueue.Peek().IOState == IOState.Idle) // I/O 操作完成
				{
					process = ioBlockedQueue.Dequeue();
					process.State = ProcessState.HighPriorityReady;
					Log($"进程退出 I/O 阻塞队列，进入高优先就绪队列：{process}");
					highPriorityQueue.Enqueue(process);
				}
			}

			process = null;
			if (highPriorityQueue.Count != 0)
			{
				process = highPriorityQueue.Dequeue();
				process.TimeSlice = 100;
			}
			else if (lowPriorityQueue.Count != 0)
			{
				process = lowPriorityQueue.Dequeue();
				process.TimeSlice = 500;
			}
			if (process == null)
			{
				return;
			}
			process.State = ProcessState.Running;
			Log("上下文切换");
			Log($"进程进入运行态：{process}");
			await process.Run();
			if (process.IOState == IOState.Requesting) // 请求 I/O
			{
				process.State = ProcessState.IOBlocked;
				Log($"进程请求 I/O，进入 I/O 阻塞队列：{process}");
				ioBlockedQueue.Enqueue(process);
			}
			else if (process.State == ProcessState.Running)
			{
				if (process.TimeRemaining > 0) // 时间片到
				{
					process.State = ProcessState.LowPriorityReady;
					Log($"进程时间片到，进入低优先就绪队列：{process}");
					lowPriorityQueue.Enqueue(process);
				}
				else // 执行结束
				{
					process.State = ProcessState.Completed;
					Log($"进程结束：{process}");
				}
			}
		}
	}
}
