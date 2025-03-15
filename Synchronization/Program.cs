
using Synchronization.Interfaces;
using Synchronization.Models;
using Synchronization.Services;
using Synchronization.Utils;
namespace Synchronization
{
    class Program
    {
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();
        static async Task Main(string[] args)
        {
            CtsUtils.ConnectCtsToConsole(cts);
            var input = ParserUtils.ParserConsoleArguments(args);
            if (input == null)
            {
                Console.WriteLine("Error: Invalid input parameters.");
                return;
            }

            var logger = new Logger();
            var fileSynchronizer = new FileSynchronizer(logger);
            var starter = new Starter(logger, fileSynchronizer);
            await starter.RunAsync(input, cts);
        }
    }
}
