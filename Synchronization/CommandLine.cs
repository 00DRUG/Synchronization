using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

namespace Synchronization
{
    public class CommandLineOptions
    {
        [Option('s', "source", Required = true, HelpText = "Source directory path.")]
        public string SourceDirectory { get; set; } = string.Empty;

        [Option('t', "target", Required = true, HelpText = "Target directory path.")]
        public string TargetDirectory { get; set; } = string.Empty;

        [Option('l', "log", Required = true, HelpText = "Path to the log file.")]
        public string LogFilePath { get; set; } = string.Empty;

        [Option('m', "method", Default = "Binary", HelpText = "Comparison method (MD5, SHA256, Binary).")]
        public string ComparisonMethod { get; set; }= string.Empty;

        [Option('d', "delay", Default = 10000, HelpText = "Synchronization delay in milliseconds.")]
        public int SyncDelay { get; set; }
    }

    public class CommandLine
    {
        private Logger? _logger;
        private FileSynchronizer? _fileSynchronizer;

        public async Task RunAsync(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args);

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

        private static bool ValidateOptions(CommandLineOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.SourceDirectory) ||
                string.IsNullOrWhiteSpace(options.TargetDirectory) ||
                string.IsNullOrWhiteSpace(options.LogFilePath) ||
                options.SyncDelay < 1000) // Minimum delay of 1 second
            {
                Console.WriteLine("Error: Invalid parameters. Ensure all required values are provided and delay is at least 1000ms.");
                return false;
            }

            return true;
        }
    }
}
