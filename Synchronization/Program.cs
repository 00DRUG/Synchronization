
namespace Synchronization
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var commandLine = new CommandLine();
            await commandLine.RunAsync(args);
        }
    }
}
