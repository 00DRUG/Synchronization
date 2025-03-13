using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Synchronization
{
    public enum ComparisonMethod
    {
        MD5,
        SHA256,
        None,
        Binary
    }

    public class FileSynchronizer(string sourcePath, string targetPath, Logger logger, ComparisonMethod comparisonMethod, CancellationTokenSource cancellationToken, int syncDelay = 10000)
    {
        private const int MaxRetryAttempts = 5;
        private const int RetryDelayMilliseconds = 1000;
        private readonly int syncDelay = syncDelay > 0 ? syncDelay : throw new ArgumentException("Sync delay must be greater than 0.", nameof(syncDelay));

        private readonly string sourcePath= EnsureTrailingSlash(Path.GetFullPath(sourcePath));
        private readonly string targetPath = EnsureTrailingSlash(Path.GetFullPath(targetPath));
        private readonly Logger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ComparisonMethod _comparisonMethod = comparisonMethod;
        private readonly CancellationTokenSource _cancellationToken = cancellationToken;



        // Ensures a path has a trailing slash
        private static string EnsureTrailingSlash(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;

        public async Task StartAsync()
        {
            _logger.LogSyncStart("Starting synchronization.");
            try
            {
                while (!_cancellationToken.Token.IsCancellationRequested)
                {
                    await SyncDirectoriesAsync();
                    await Task.Delay(syncDelay, _cancellationToken.Token); 
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Synchronization operation was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected error during synchronization: {ex.Message}");
            }
            finally
            {
                _logger.LogSyncEnd("Synchronization completed.");
            }
        }

        private async Task SyncDirectoriesAsync()
        {
            try
            {
                DirectoryInfo sourceDirectory = new(sourcePath);
                DirectoryInfo targetDirectory = new(targetPath);

                if (!sourceDirectory.Exists)
                {
                    _logger.Error($"Source directory '{sourcePath}' does not exist.");
                    return;
                }

                if (!targetDirectory.Exists)
                {
                    _logger.Warning($"Target directory '{targetPath}' does not exist. Creating...");
                    targetDirectory.Create();
                }

                // Perform sync operations concurrently
                Task syncTask = SyncFilesAndDirectoriesAsync(sourceDirectory);
                Task cleanTask = CleanDirectoryAsync(targetDirectory);
                await Task.WhenAll(syncTask, cleanTask);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during synchronization: {ex.Message}");
            }
        }

        private async Task SyncFilesAndDirectoriesAsync(DirectoryInfo sourceDirectory)
        {
            // Ensure all directories exist in the target
            await SyncDirectoriesAsync(sourceDirectory);

            // Sync files asynchronously
            IEnumerable<FileInfo> files = sourceDirectory.GetFiles("*", SearchOption.AllDirectories);
            IEnumerable<Task> fileTasks = files.Select(file => ProcessFileAsync(file));

            await Task.WhenAll(fileTasks);
        }

        private async Task SyncDirectoriesAsync(DirectoryInfo sourceDirectory)
        {
            IOrderedEnumerable<DirectoryInfo> directories = sourceDirectory
                .GetDirectories("*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.FullName.Length); // Ensure parent directories are created first

            foreach (var dir in directories)
            {
                string targetDirPath = dir.FullName.Replace(sourcePath,targetPath);
                if (!Directory.Exists(targetDirPath))
                {
                    Directory.CreateDirectory(targetDirPath);
                    _logger.LogAdd(targetDirPath, $"Directory created: '{targetDirPath}' (source directory: '{dir.FullName}')");
                }
            }
        }

        private async Task ProcessFileAsync(FileInfo sourceFile)
        {
            string targetFilePath = sourceFile.FullName.Replace(sourcePath, targetPath);
            FileInfo targetFile = new(targetFilePath);

            try
            {
                if (await ShouldCopyFileAsync(sourceFile, targetFile))
                {
                    if (!targetFile.Exists)
                    {
                        _logger.LogAdd(targetFilePath, $"File {sourceFile.Name} added from '{sourceFile.FullName}' to '{targetFilePath}'");
                    }
                    else _logger.LogUpdate(targetFilePath, $"File updated from '{sourceFile.FullName}' to '{targetFilePath}'");
                    await CopyFileAsync(sourceFile.FullName, targetFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error syncing file '{sourceFile.FullName}': {ex.Message}");
            }
        }

        private async Task<bool> ShouldCopyFileAsync(FileInfo sourceFile, FileInfo targetFile)
        {
            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                try
                {
                    if (!targetFile.Exists ||
                        sourceFile.LastWriteTime > targetFile.LastWriteTime ||
                        sourceFile.Length != targetFile.Length ||
                        !await CompareFilesAsync(sourceFile.FullName, targetFile.FullName))
                    {
                        return true;
                    }
                    return false;
                }
                catch (IOException ex) when (attempt < MaxRetryAttempts - 1)
                {
                    _logger.Warning($"Attempt {attempt + 1} failed: {ex.Message}. Retrying...");
                    await Task.Delay(RetryDelayMilliseconds);
                }
            }
            return false;
        }

        private async Task CopyFileAsync(string source, string target)
        {
            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                try
                {
                    using FileStream sourceStream = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using FileStream targetStream = new(target, FileMode.Create, FileAccess.Write, FileShare.None);
                    await sourceStream.CopyToAsync(targetStream);
                    return;
                }
                catch (IOException ex) when (attempt < MaxRetryAttempts - 1)
                {
                    _logger.Warning($"Attempt {attempt + 1} failed: {ex.Message}. Retrying...");
                    await Task.Delay(RetryDelayMilliseconds);
                }
            }
        }

        private async Task CleanDirectoryAsync(DirectoryInfo targetDirectory)
        {
            // Delete files asynchronously
            IEnumerable<FileInfo> files = targetDirectory.GetFiles("*", SearchOption.AllDirectories);
            IEnumerable<Task> fileTasks = files.Select(file => DeleteFileIfNotExistsInSourceAsync(file, sourcePath, targetPath));
            await Task.WhenAll(fileTasks);

            // Delete directories asynchronously (from inner to outer)
            IOrderedEnumerable<DirectoryInfo> directories = targetDirectory
                .GetDirectories("*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.FullName.Length);

            foreach (var dir in directories)
            {
                try
                {
                    string sourceDirPath = dir.FullName.Replace(targetPath, sourcePath);
                    if (!Directory.Exists(sourceDirPath))
                    {
                        _logger.LogDelete(dir.FullName, $"Directory deleted: '{dir.FullName}' (source directory '{sourceDirPath}' does not exist)");
                        await Task.Run(() => dir.Delete(true));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error deleting directory '{dir.FullName}': {ex.Message}");
                }
            }
        }

        private async Task DeleteFileIfNotExistsInSourceAsync(FileInfo targetFile, string sourcePath, string targetPath)
        {
            try
            {
                string sourceFilePath = targetFile.FullName.Replace(targetPath, sourcePath);
                if (!File.Exists(sourceFilePath))
                {
                    await Task.Run(() => targetFile.Delete());
                    _logger.LogDelete(targetFile.FullName, $"File deleted: '{targetFile.FullName}' (source file '{sourceFilePath}' does not exist)");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting file '{targetFile.FullName}': {ex.Message}");
            }
        }
        private async Task<bool> CompareFilesAsync(string filePath1, string filePath2)
        {
            return _comparisonMethod switch
            {
                ComparisonMethod.MD5 => await CompareMD5Async(filePath1, filePath2),
                ComparisonMethod.SHA256 => await CompareSHA256Async(filePath1, filePath2),
                ComparisonMethod.None => true,
                _ => await CompareFilesBinaryAsync(filePath1, filePath2)
            };
        }

        public static async Task<bool> CompareMD5Async(string filePath1, string filePath2)
        {
            using MD5 md5 = MD5.Create();
            using FileStream stream1 = new(filePath1, FileMode.Open, FileAccess.Read);
            using FileStream stream2 = new(filePath2, FileMode.Open, FileAccess.Read);

            byte[] hash1 = await md5.ComputeHashAsync(stream1);
            byte[] hash2 = await md5.ComputeHashAsync(stream2);
            return hash1.SequenceEqual(hash2);
        }

        public static async Task<bool> CompareSHA256Async(string filePath1, string filePath2)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream1 = new(filePath1, FileMode.Open, FileAccess.Read);
            using FileStream stream2 = new(filePath2, FileMode.Open, FileAccess.Read);

            byte[] hash1 = await sha256.ComputeHashAsync(stream1);
            byte[] hash2 = await sha256.ComputeHashAsync(stream2);
            return hash1.SequenceEqual(hash2);
        }
        private async Task<bool> CompareFilesBinaryAsync(string filePath1, string filePath2)
        {
            const int bufferSize = 1024*1024; // 1 mb buffer size for efficient reading

            try
            {
                using (FileStream stream1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous))
                using (FileStream stream2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous))
                {
                    if (stream1.Length != stream2.Length)
                    {
                        return false; // Files are different sizes
                    }

                    byte[] buffer1 = new byte[bufferSize];
                    byte[] buffer2 = new byte[bufferSize];

                    while (true)
                    {
                        int bytesRead1 = await stream1.ReadAsync(buffer1, 0, bufferSize);
                        int bytesRead2 = await stream2.ReadAsync(buffer2, 0, bufferSize);

                        if (bytesRead1 != bytesRead2)
                        {
                            return false; // Files have different content lengths
                        }

                        if (bytesRead1 == 0)
                        {
                            return true; // End of both files reached, and all bytes matched
                        }

                        if (!buffer1.SequenceEqual(buffer2))
                        {
                            return false; // Bytes do not match
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error comparing files '{filePath1}' and '{filePath2}': {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            _logger.Info("Stopping synchronization.");
            _cancellationToken.Cancel();
        }
    }
}