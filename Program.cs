namespace Synchronization
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var logger = new Logger("log.txt");
            logger.StartLogging();

            var synchronizer = new FileSynchronizer(logger, ComparisonMethod.MD5);
            var cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await synchronizer.SynchronizeDirectoriesAsync(@"C:\Source", @"C:\Target", cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                logger.Warning("Operation was canceled.");
            }
            finally
            {
                logger.StopLogging();
            }
        }
    }
}
