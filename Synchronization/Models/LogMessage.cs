using Synchronization.Enums;

namespace Synchronization.Models;
public class LogMessage
{
    public DateTime Timestamp { get; }
    public string Message { get; }
    public LogLevel Level { get; }
    public OperationType Operation { get; }

    public LogMessage(string message, LogLevel level, OperationType operation = OperationType.Info)
    {
        Timestamp = DateTime.Now;
        Message = message;
        Level = level;
        Operation = operation;
    }
}