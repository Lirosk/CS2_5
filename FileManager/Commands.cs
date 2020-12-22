using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileManager
{
	//from lab1...
	internal class Commands
	{
		private readonly string targetDirectoryPath;
		private readonly string logPath;

		public Commands(Options options)
		{
			targetDirectoryPath = options.TargetDirectoryPath;
			logPath = options.LogPath;
		}

		internal void OnAdded(object sender, FileSystemEventArgs e)
		{
			ThreadPool.QueueUserWorkItem(Added, e.FullPath);
		}

		private async void Added(object state)
		{
			var path = (string)state;
			var sourceDirectoryPath = Path.GetDirectoryName(path);
			var name = Path.GetFileName(path);

			try
			{
				//waiting for the file to become free
				Overseer.GetStreamWriter(path, 10_000).Dispose();

				//to zip
				Manager.overseer.watcher.EnableRaisingEvents = false;
				await ZipAsync(sourceDirectoryPath, sourceDirectoryPath, name);
				await DeleteAsync(sourceDirectoryPath, name);
				Manager.overseer.watcher.EnableRaisingEvents = true;

				//encrypt
				await EncryptAsync(sourceDirectoryPath, name + ".gz"); //because Zip(...) adds ".gz" to zipped file name

				//move
				await MoveAsync(sourceDirectoryPath, targetDirectoryPath, name + ".gz");

				//unzip
				await DecryptAsync(targetDirectoryPath, name + ".gz");
				await UnzipAsync(targetDirectoryPath, targetDirectoryPath, name + ".gz");
				DeleteAsync(targetDirectoryPath, name + ".gz");
			}
			catch (IOException ex)
			{
				using (StreamWriter writer = new StreamWriter(logPath, true))
				{
					writer.WriteLine("\nException!\n" + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss ") + ex.Message + "\n\n");
					writer.Flush();
				}

				DeleteAsync(sourceDirectoryPath, name + ".gz");
			}
		}

		private async Task UnzipAsync(string sourceDirectoryPath, string targetDirectoryPath, string name)
		{
			await Task.Run(() =>
			{
				var buf = name.Substring(0, name.Length - 3);//[..^3];

				using (var targetStream = File.Create(Path.Combine(targetDirectoryPath, buf)))
				using (var sourceStream = File.OpenRead(Path.Combine(sourceDirectoryPath, name)))
				using (var decomressionStream = new GZipStream(sourceStream, CompressionMode.Decompress))
				{
					decomressionStream.CopyTo(targetStream);
				}
			});
		}

		private async Task MoveAsync(string sourcePath, string targetPath, string name)
		{
			await Task.Run(async () =>
			{
				if (File.Exists(Path.Combine(targetPath, name)))
				{
					await DeleteAsync(targetPath, name);
				}

				File.Move(Path.Combine(sourcePath, name), Path.Combine(targetPath, name));
			});
		}

		private async Task DeleteAsync(string filePath, string name)
		{
			await Task.Run(() =>
				File.Delete(Path.Combine(filePath, name))
			);
		}

		private async Task ZipAsync(string sourcePath, string targetPath, string name)
		{
			await Task.Run(() =>
			{
				var buf = new StringBuilder(name);
				buf.Append(".gz");

				using (var targetStream = File.Create(Path.Combine(targetPath, buf.ToString())))
				using (var sourceStream = new FileStream(Path.Combine(sourcePath, name), FileMode.OpenOrCreate, FileAccess.Read))
				using (var compressionStream = new GZipStream(targetStream, CompressionMode.Compress))
				{
					sourceStream.CopyTo(compressionStream);
				}
			});
		}

		private async Task EncryptAsync(string filePath, string name)
		{
			await Task.Run(() =>
			{
				File.Encrypt(Path.Combine(filePath, name));
			});
		}

		private async Task DecryptAsync(string filePath, string name)
		{
			await Task.Run(() =>
			{
				File.Decrypt(Path.Combine(filePath, name));
			});
		}
	}
}
