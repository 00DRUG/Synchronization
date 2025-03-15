using Synchronization.Extensions;
using Synchronization.Interfaces;
using Synchronization.Models;
using System.Threading;

namespace Synchronization.Services;
public class Starter
{
    private ILogger _logger;
    private ISynchronizer _Synchronizer;
    public Starter(ILogger logger, ISynchronizer Synchronizer)
    {
        _logger = logger;
        _Synchronizer = Synchronizer;
    }   
    public async Task RunAsync(InputParameters input, CancellationTokenSource cancellationTokenSource)
    {
        var logger = new Logger(input.LogFilePath);
        try
        {
            await this._Synchronizer.StartAsync(input, cancellationTokenSource);
        }
        catch (OperationCanceledException)
        {
            this._logger.Warning("Operation was canceled.");
        }
        catch (Exception ex)
        {
            this._logger.Error($"An error occurred: {ex.Message}");
        }
        finally
        {
            this._logger.LogSyncEnd("Process stopped");
        }
    }
}