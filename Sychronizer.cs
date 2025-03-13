using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Synchronization.Logger;
namespace Synchronization
{
    public enum ComparisonMethod
    {
        MD5,
        SHA256,
        None,
        Binary
    }
    public class FileSynchronizer
    {
        private readonly Logger logger;
        private readonly ComparisonMethod comparingMethod;

        public FileSynchronizer(Logger logger, ComparisonMethod comparingMethod = ComparisonMethod.Binary)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.comparingMethod = comparingMethod;
        }

        public async Task SynchronizeDirectoriesAsync(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sourceDirectory) || string.IsNullOrEmpty(targetDirectory))
                throw new ArgumentException("Source or target directory cannot be null or empty.");

            if (!Directory.Exists(sourceDirectory) || !Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException("One or both directories do not exist.");

            var sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            var targetFiles = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories);

            var tasks = new List<Task>();

            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = sourceFile.Substring(sourceDirectory.Length);
                var targetFile = Path.Combine(targetDirectory, relativePath);

                tasks.Add(CompareAndSyncFilesAsync(sourceFile, targetFile, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task CompareAndSyncFilesAsync(string sourceFile, string targetFile, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.Warning($"Operation cancelled: {sourceFile} to {targetFile}");
                return;
            }

            bool filesAreIdentical = await CompareFilesAsync(sourceFile, targetFile, cancellationToken);
            if (!filesAreIdentical)
            {
                await SyncFileAsync(sourceFile, targetFile);
            }
            else
            {
                logger.Info($"{sourceFile} and {targetFile} are identical.");
            }
        }

        private async Task<bool> CompareFilesAsync(string filePath1, string filePath2, CancellationToken cancellationToken)
        {
            bool result = comparingMethod switch
            {
                ComparisonMethod.MD5 => await CompareMD5Async(filePath1, filePath2, cancellationToken),
                ComparisonMethod.SHA256 => await CompareSHA256Async(filePath1, filePath2, cancellationToken),
                ComparisonMethod.None => await CompareByFileAttributesAsync(filePath1, filePath2), // Use file attributes like size, date
                _ => await CompareFilesBinaryAsync(filePath1, filePath2, cancellationToken), // Default to binary comparison
            };

            if (!result)
            {
                logger.Warning($"{filePath1} and {filePath2} didn't match");
            }

            return result;
        }

        private async Task<bool> CompareByFileAttributesAsync(string filePath1, string filePath2)
        {
            var file1 = new FileInfo(filePath1);
            var file2 = new FileInfo(filePath2);

            return file1.Length == file2.Length && file1.LastWriteTime == file2.LastWriteTime;
        }

        private async Task<bool> CompareMD5Async(string filePath1, string filePath2, CancellationToken cancellationToken)
        {
            var hash1 = await ComputeMD5Async(filePath1, cancellationToken);
            var hash2 = await ComputeMD5Async(filePath2, cancellationToken);
            return hash1 == hash2;
        }

        private async Task<bool> CompareSHA256Async(string filePath1, string filePath2, CancellationToken cancellationToken)
        {
            var hash1 = await ComputeSHA256Async(filePath1, cancellationToken);
            var hash2 = await ComputeSHA256Async(filePath2, cancellationToken);
            return hash1 == hash2;
        }

        private async Task<bool> CompareFilesBinaryAsync(string filePath1, string filePath2, CancellationToken cancellationToken)
        {
            const int bufferSize = 8192;

            using (var stream1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var stream2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var buffer1 = new byte[bufferSize];
                var buffer2 = new byte[bufferSize];

                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    int bytesRead1 = await stream1.ReadAsync(buffer1, 0, bufferSize, cancellationToken);
                    int bytesRead2 = await stream2.ReadAsync(buffer2, 0, bufferSize, cancellationToken);

                    if (bytesRead1 != bytesRead2 || !CompareByteArrays(buffer1, buffer2, bytesRead1))
                        return false;

                    if (bytesRead1 == 0 && bytesRead2 == 0)
                        return true;
                }
            }
        }

        private bool CompareByteArrays(byte[] array1, byte[] array2, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (array1[i] != array2[i])
                    return false;
            }
            return true;
        }

        private async Task<string> ComputeMD5Async(string filePath, CancellationToken cancellationToken)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = await md5.ComputeHashAsync(stream, cancellationToken);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task<string> ComputeSHA256Async(string filePath, CancellationToken cancellationToken)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task SyncFileAsync(string sourceFile, string targetFile)
        {
            try
            {
                var targetDirectory = Path.GetDirectoryName(targetFile);
                if (!Directory.Exists(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                await Task.Run(() => File.Copy(sourceFile, targetFile, overwrite: true));
                logger.Info($"Synchronized {sourceFile} to {targetFile}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error synchronizing {sourceFile} to {targetFile}: {ex.Message}");
            }
        }
    }
}