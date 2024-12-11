using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PlazaEngine.Engine
{

	/// <summary>
	/// Логth для Роутера Plaza
	/// </summary>
	public static class RouterLogger
	{
		private struct QueueMessage
		{
			public string logName;
			public string message;
		}

		private static ConcurrentQueue<QueueMessage> concurrentQueueMessage = new ConcurrentQueue<QueueMessage>();
		private static Thread threadBLoger = new Thread(LogSaver);


		private static void LogSaver()
		{
			Dictionary<string, StreamWriter> LogFiles = new Dictionary<string, StreamWriter>();
			string path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\RouterPlaza\\log\\";

			DateTime timeSave = DateTime.Now;
			DateTime timeDelete = DateTime.Now;
			bool timeDeleteUpdate = false;
			while (!cancellationToken.IsCancellationRequested)
			{
				Thread.Sleep(10);

				if (timeSave.AddSeconds(5) < DateTime.Now)  // автосохранение 
				{
					foreach (var logFiles in LogFiles.ToFrozenDictionary())
					{
						if (timeDelete.AddSeconds(60) < DateTime.Now)
						{
							string fileName = GetFileName(path, logFiles.Key);
							if (File.Exists(fileName) && new FileInfo(fileName).Length > 300 * 1000000)
							{
								logFiles.Value.Close();
								File.Delete(fileName);
								LogFiles[logFiles.Key] = new StreamWriter(fileName, true);
							}
							else if (!File.Exists(fileName))
							{

							}
							timeDeleteUpdate = true;
						}
						logFiles.Value.Flush();
					}
					timeSave = DateTime.Now;
					if (timeDeleteUpdate)
					{
						timeDelete = DateTime.Now;
						timeDeleteUpdate = false;
					}
				}
				try
				{
					while (!(concurrentQueueMessage?.IsEmpty ?? true))
					{
						if (concurrentQueueMessage.TryDequeue(out QueueMessage message))
						{
							if (!LogFiles.ContainsKey(message.logName))
							{
								string fileName = GetFileName(path, message.logName);
								LogFiles[message.logName] = new StreamWriter(fileName, true);
							}
							LogFiles[message.logName].WriteLine(message.message);
						}
					}

				}
				catch (Exception ex)
				{
				}
			}

			foreach (var item in LogFiles)
			{
				try
				{
					item.Value.Close();
				}
				catch
				{
					// игнор
				}
			}
		}

		private static string GetFileName(string path, string Key)
		{
			return path + Key + "_" + DateTime.Now.ToString("yyyy.MM.dd") + @".txt";
		}

		private static CancellationTokenSource tokenSource = new CancellationTokenSource();
		private static CancellationToken cancellationToken = tokenSource.Token;

		public static void Dispose()
		{
			tokenSource.Cancel();
		}

		/// <summary>
		/// Записать сообщение в файл Exeption.txt
		/// </summary>
		/// <param name="message"></param>
		/// <param name="tag"></param>
		/// <param name="sourceFilePath"></param>
		/// <param name="sourceLineNumber"></param>
		public static void Log(string message, [CallerMemberName] string tag = "",
												[CallerFilePath] string sourceFilePath = "",
												[CallerLineNumber] int sourceLineNumber = 0)
		{
			try
			{

				DateTime now = DateTime.UtcNow.AddHours(3); //MCK
				string trimMessage;
				if (message.Length > 5000)
				{
					trimMessage = message.Substring(0, 2500) + "...<< cut " + (message.Length - 5000).ToString() + " >>..." + message.Substring(message.Length - 2500, 2500);
				}
				else
				{
					trimMessage = message;
				}
				string modul = Path.GetFileName(sourceFilePath) ?? "";
				string outMessage = $"{now:yyyy.MM.dd}; {now.ToLongTimeString()}.{now.Millisecond:D3}; {modul}; {tag}; {sourceLineNumber}; {trimMessage}";

				concurrentQueueMessage.Enqueue(new QueueMessage { message = outMessage, logName = "Exeption" });

				if (threadBLoger.ThreadState == ThreadState.Unstarted)
				{
					threadBLoger.Start();
					threadBLoger.IsBackground = true;
					concurrentQueueMessage.Enqueue(new QueueMessage { message = "\n\n", logName = "Exeption" });
				}
			}
			catch
			{

			}
		}

		/// <summary>
		/// Записать сообщение в файл с именем, указанным в LogName
		/// </summary>
		/// <param name="Message"></param>
		/// <param name="LogName"></param>
		public static void Log(string Message, string LogName)
		{
			try
			{
				DateTime now = DateTime.UtcNow.AddHours(3); //MCK
				string messageSend = $"{now:yyyy.MM.dd}; {now.ToLongTimeString()}.{now.Millisecond:D3}; {Message}";
				concurrentQueueMessage.Enqueue(new QueueMessage { message = messageSend, logName = LogName });

				if (threadBLoger.ThreadState == ThreadState.Unstarted)
				{
					threadBLoger.Start();
					threadBLoger.IsBackground = true;
					concurrentQueueMessage.Enqueue(new QueueMessage { message = "\n\n", logName = LogName });
				}
			}
			catch
			{

			}
		}
	}

}
