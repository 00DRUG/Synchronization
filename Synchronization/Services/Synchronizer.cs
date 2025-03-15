using Synchronization.Enums;
using Synchronization.Interfaces;
using Synchronization.Models;
using Synchronization.Utils;
namespace Synchronization.Services;


public class FileSynchronizer : ISynchronizer
{
    private readonly ILogger _logger;
    private const int MaxRetryAttempts = 10;
    private const int RetryDelayMilliseconds = 1000;
    private InputParameters _inputPrameters;

    public FileSynchronizer(ILogger logger)
    {
        _logger = logger;
        _inputPrameters = new InputParameters();
    }
    public async Task StartAsync(InputParameters input, CancellationToken cancellationToken)
    {
        _inpuParameters = input;
        _logger.LogSyncStart("Starting synchronization.");
        try
        {
            var context = new FilesContext(input.SourceDirectory, input.TargetDirectory);
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                await SyncDirectoriesAsync();
                await Task.Delay(input.SyncDelay, cancellationToken.Token);
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

    private Task SyncDirectoriesAsync(DirectoryInfo sourceDirectory)
    {
        IOrderedEnumerable<DirectoryInfo> directories = sourceDirectory
            .GetDirectories("*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.FullName.Length); // Ensure parent directories are created first

        foreach (var dir in directories)
        {
            string targetDirPath = dir.FullName.Replace(sourcePath, targetPath);
            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
                _logger.LogAdd(targetDirPath, $"Directory created: '{targetDirPath}' (source directory: '{dir.FullName}')");
            }
        }

        return Task.CompletedTask;
    }

    private async Task ProcessFileAsync(FileInfo sourceFile)
    {
        string targetFilePath = sourceFile.FullName.Replace(sourcePath, targetPath);
        FileInfo targetFile = new(targetFilePath);

        try
        {
            if (await ShouldCopyFileAsync(sourceFile, targetFile))
            {
                if (!targetFile.Exists) _logger.LogAdd(targetFilePath, $"File {sourceFile.Name} added from '{sourceFile.FullName}' to '{targetFilePath}'");
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
    private async Task<bool> CompareFilesAsync(string filePath1, string filePath2, CancellationToken cancellationToken)
        => _inputParameters.ComparisonMethod switch
        {
            ComparisonMethod.MD5 => await Comparators.CompareMD5Async(filePath1, filePath2, cancellationToken),
            ComparisonMethod.SHA256 => await Comparators.CompareSHA256Async(filePath1, filePath2, cancellationToken),
            ComparisonMethod.None => true,
            ComparisonMethod.Binary => await Comparators.CompareFilesBinaryAsync(filePath1, filePath2, cancellationToken),
            _ => throw new ArgumentException()
        };

}