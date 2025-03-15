using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Synchronization.Models;
using Synchronization.Services;

namespace Synchronization
{
    public class CommandLine
    {
        private Logger _logger;
        private FileSynchronizer _fileSynchronizer;

        public async Task RunAsync(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<InputParameters>(args);

            await parserResult.MapResult(
                async options =>
                {
                    // Validate input parameters
                    if (!ValidateOptions(options))
                    {
                        Console.WriteLine("Error: Invalid input parameters.");
                        return;
                    }

                    _logger = new Logger(options.LogFilePath);
                    _logger.StartLogging();

                    try
                    {
                        // Parse the comparison method
                        ComparisonMethod comparisonMethod = Enum.TryParse(options.ComparisonMethod, true, out ComparisonMethod parsedMethod)
                            ? parsedMethod
                            : ComparisonMethod.Binary;

                        using CancellationTokenSource cts = new();
                        _fileSynchronizer = new FileSynchronizer(options.SourceDirectory, options.TargetDirectory, _logger, comparisonMethod, cts, options.SyncDelay);

                        // Start synchronization with user-defined delay
                        _logger.Info($"Starting synchronization with a delay of {options.SyncDelay}ms.");
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await _fileSynchronizer.StartAsync();
                            await Task.Delay(options.SyncDelay, cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warning("Operation was canceled.");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"An error occurred: {ex.Message}");
                    }


                    finally
                    {
                        _logger.StopLogging();
                    }
                },
                _ => Task.CompletedTask // Handle parsing errors
            );
        }
    }
}
