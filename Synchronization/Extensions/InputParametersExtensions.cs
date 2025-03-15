using Synchronization.Models;

namespace Synchronization.Extensions;
public static class InputParametersExtensions
{
    private static bool ValidateAndRefill(InputParameters options)
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