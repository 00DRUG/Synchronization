using CommandLine;
using Synchronization.Enums;

namespace Synchronization.Models;
public class InputParameters
{
    [Option('s', "source", Required = true, HelpText = "Source directory path.")]
    public string SourceDirectory { get; set; } = string.Empty;

    [Option('t', "target", Required = true, HelpText = "Target directory path.")]
    public string TargetDirectory { get; set; } = string.Empty;

    [Option('l', "log", Required = true, HelpText = "Path to the log file.")]
    public string LogFilePath { get; set; } = string.Empty;

    [Option('m', "method", Default = "Binary", HelpText = "Comparison method (MD5, SHA256, Binary).")]
    public  ComparisonMethod ComparisonMethod { get; set; } = ComparisonMethod.Binary;

    [Option('d', "delay", Default = 10000, HelpText = "Synchronization delay in milliseconds.")]
    public int SyncDelay { get; set; }
}
