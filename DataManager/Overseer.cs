using System;
using System.Threading;
using System.IO;
using DataAccess;
using ServiceLayer;
using Models.Result;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DataManager
{
	class Overseer
	{
		private bool enabled = true;
		private readonly FileSystemWatcher watcher;
		private readonly string connectionString;
		private readonly string targetDirectorPath;
		private readonly string tasksDirectoryPath;
		private readonly string logPath;
		private readonly bool deleteFile;
		private readonly DBAccess dbAccess;

		public Overseer(Options options)
		{			
			connectionString = options.ConnectionString;

			targetDirectorPath = options.TargetDirectoryPath;
			tasksDirectoryPath = options.TasksDirectoryPath;
			logPath = options.LogPath;

			deleteFile = options.DeleteFile;

			dbAccess = new DBAccess(connectionString);
			watcher = new FileSystemWatcher(tasksDirectoryPath);

			watcher.Error += Watcher_Error;
			watcher.Deleted += Watcher_Deleted;
			watcher.Created += Watcher_AddedAsync;
			if (deleteFile)
			{
				watcher.Created += DeleteAsync;
			}
			watcher.Changed += Watcher_Changed;
			watcher.Renamed += Watcher_Renamed;
		}

		public async void Watcher_AddedAsync(object sender, FileSystemEventArgs e)
		{
			ThreadPool.QueueUserWorkItem(OnAdded, e.FullPath);
		}

		public void OnAdded(object state)
		{
			var path = (string)state;
			try
			{
				//waiting for the file to become free
				GetStreamWriter(path, 10_000).Dispose();

				var result = GetResult(path);
				Layer.GenerateResultFiles(result, targetDirectorPath, "result");
			}
			catch (Exception ex)
			{
				Watcher_Error(this, new ErrorEventArgs(ex));
			}
		}

		private Result<object> GetResult(string file)
		{
			return dbAccess.GetTable<object>(file);
		}

		private async void DeleteAsync(object sender, FileSystemEventArgs e)
		{
			await Task.Run(() =>
			{
				Thread.Sleep(100);
				//waiting for the file to become free
				GetStreamWriter(e.FullPath, 10_000).Dispose();

				File.Delete(e.FullPath);
			});
		}

		private void Delete(string path)
		{
			File.Delete(path);
		}

		public void Start()
		{
			watcher.EnableRaisingEvents = true;
			while (enabled)
			{
				Thread.Sleep(1000);
			}
		}

		public void Stop()
		{
			watcher.EnableRaisingEvents = false;
			enabled = false;
		}

		private void Watcher_Renamed(object sender, RenamedEventArgs e)
		{
			string fileEvent = "renamed to " + e.FullPath;
			string filePath = e.OldFullPath;
			RecordEntryAsync(fileEvent, filePath);
		}

		private void Watcher_Changed(object sender, FileSystemEventArgs e)
		{
			string fileEvent = "changed";
			string filePath = e.FullPath;
			RecordEntryAsync(fileEvent, filePath);
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			string fileEvent = "created";
			string filePath = e.FullPath;
			RecordEntryAsync(fileEvent, filePath);
		}

		private void Watcher_Deleted(object sender, FileSystemEventArgs e)
		{
			string fileEvent = "deleted";
			string filePath = e.FullPath;
			RecordEntryAsync(fileEvent, filePath);
		}

		private void Watcher_Error(object sender, ErrorEventArgs e)
		{
			using (StreamWriter writer = new StreamWriter(logPath, true))
			{
				writer.WriteLine("\nException!\n" + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss ") + e.GetException().Message + "\n\n");
				writer.Flush();
			}
		}

		private async Task RecordEntryAsync(string fileEvent, string filePath)
		{
			await Task.Run(() =>
			{
				using (StreamWriter writer = GetStreamWriter(logPath, 10_000))
				{
					writer.WriteLine(String.Format("{0} file {1} was {2}\n",
						DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss"), filePath, fileEvent));
					writer.Flush();
				}
			});
		}

		private StreamWriter GetStreamWriter(string path, int timeoutMs)
		{
			var time = Stopwatch.StartNew();
			while (time.ElapsedMilliseconds < timeoutMs)
			{
				try
				{
					return new StreamWriter(path, true);
				}
				catch (IOException e)
				{
					// access error
					if (e.HResult != -2147024864)
						throw;
				}
			}

			throw new TimeoutException($"Failed to get a write handle to {path} within {timeoutMs}ms.");
		}
	}
}
