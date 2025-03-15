namespace Synchronization.Utils;

public static class CtsUtils
{
    public static void ConnectCtsToConsole(CancellationTokenSource cts)
    {
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Cancel event triggered");
            cts.Cancel();
            eventArgs.Cancel = true;
        };
    }
}