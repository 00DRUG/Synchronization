using Synchronization.Enums;
using Synchronization.Extensions;
using Synchronization.Interfaces;
using Synchronization.Models;
using Synchronization.Utils;
namespace Synchronization.Services;


public class FileSynchronizer : ISynchronizer
{
    private readonly ILogger _logger;
    private const int MaxRetryAttempts = 10;
    private const int RetryDelayMilliseconds = 1000;
    private InputParameters _inputParameters;

    public FileSynchronizer(ILogger logger)
    {
        _logger = logger;
        _inputParameters = new InputParameters();
    }
    public async Task StartAsync(InputParameters input, CancellationTokenSource cancellationToken)
    {
        _inputParameters = input;
        _logger.LogSyncStart("Starting synchronization.");
        try
        {
            var context = new FilesContext(input.SourceDirectory, input.TargetDirectory);
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                await SyncDirectoriesAsync(context, cancellationToken.Token);
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

    private async Task SyncDirectoriesAsync(FilesContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (!context.SourceDirectory.Exists)
            {
                _logger.Error($"Source directory '{context.SourceDirectory.FullName}' does not exist.");
                return;
            }

            if (!context.TargetDirectory.Exists)
            {
                _logger.Warning($"Target directory '{context.TargetDirectory.FullName}' does not exist. Creating...");
                context.TargetDirectory.Create();
            }

            await SyncFilesAndDirectoriesAsync(context, cancellationToken);
            await CleanDirectory(context.TargetDirectory, context.SourcePath, context.TargetPath);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error during synchronization: {ex.Message}");
        }
    }

    private async Task SyncFilesAndDirectoriesAsync(FilesContext context, CancellationToken cancellationToken)
    {
        await SyncDirectoriesAsync(context.SourceDirectory, context.SourcePath, context.TargetPath);
        var files = context.SourceDirectory.GetFiles("*", SearchOption.AllDirectories);
        var fileTasks = files.Select(file => ProcessFileAsync(file, context.SourcePath, context.TargetPath, cancellationToken));

        await Task.WhenAll(fileTasks);
    }

    private Task SyncDirectoriesAsync(DirectoryInfo sourceDirectory, string sourcePath, string targetPath)
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
                _logger.LogAdd( $"Directory created: '{targetDirPath}' (source directory: '{dir.FullName}')");
            }
        }

        return Task.CompletedTask;
    }

    private async Task ProcessFileAsync(FileInfo sourceFile, string sourcePath, string targetPath, CancellationToken cancellationToken)
    {
        string targetFilePath = sourceFile.FullName.Replace(sourcePath, targetPath);
        FileInfo targetFile = new(targetFilePath);

        try
        {
            if (await ShouldCopyFileAsync(sourceFile, targetFile, cancellationToken))
            {
                if (!targetFile.Exists)
                {
                    _logger.LogAdd($"File {sourceFile.Name} added from '{sourceFile.FullName}' to '{targetFilePath}'");
                }
                else
                {
                    _logger.LogUpdate($"File updated from '{sourceFile.FullName}' to '{targetFilePath}'");
                }

                await CopyFileAsync(sourceFile.FullName, targetFilePath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error syncing file '{sourceFile.FullName}': {ex.Message}");
        }
    }

    private async Task<bool> ShouldCopyFileAsync(FileInfo sourceFile, FileInfo targetFile, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                if (!targetFile.Exists ||
                    sourceFile.LastWriteTime > targetFile.LastWriteTime ||
                    sourceFile.Length != targetFile.Length ||
                    !await CompareFilesAsync(sourceFile.FullName, targetFile.FullName,cancellationToken))
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
            catch (Exception ex)
            {
                _logger.Error($"Error occured: {ex.Message}");
                return false;
            }
        }
        return false;
    }

    private async Task CopyFileAsync(string source, string target, CancellationToken cancellationToken)
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

    private async Task CleanDirectory(DirectoryInfo targetDirectory, string sourcePath, string targetPath)
    {
        var files = targetDirectory.GetFiles("*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            await DeleteFileIfNotExistsInSourceAsync(file, sourcePath, targetPath);
        }
        var directories = targetDirectory
            .GetDirectories("*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.FullName.Length);
        foreach (var dir in directories)
        {
            try
            {
                string sourceDirPath = dir.FullName.Replace(targetPath, sourcePath);
                if (!Directory.Exists(sourceDirPath))
                {
                    _logger.LogDelete( $"Directory deleted: '{dir.FullName}' (source directory '{sourceDirPath}' does not exist)");
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
                _logger.LogDelete( $"File deleted: '{targetFile.FullName}' (source file '{sourceFilePath}' does not exist)");
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