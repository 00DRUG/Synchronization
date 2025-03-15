using Synchronization.Enums;
using Synchronization.Interfaces;
using Synchronization.Models;
namespace Synchronization.Extensions;
public static class ILoggerExtensions
{
    public static void LogAdd(this ILogger logger, string message) 
        => logger.Log(new LogMessage(message, LogLevel.Info, OperationType.Add));

    public static void LogUpdate(this ILogger logger, string message) 
        => logger.Log(new LogMessage(message, LogLevel.Info, OperationType.Update));

    public static void LogDelete(this ILogger logger, string message) 
        => logger.Log(new LogMessage(message, LogLevel.Info, OperationType.Delete));

    public static void LogSyncStart(this ILogger logger, string message) 
        => logger.Log(new LogMessage(message, LogLevel.Info, OperationType.SyncStart));

    public static void LogSyncEnd(this ILogger logger, string message) 
        => logger.Log(new LogMessage(message, LogLevel.Info, OperationType.SyncEnd));

    public static void Info(this ILogger logger, string message) 
        => logger.Log(new LogMessage(message, LogLevel.Info));

    public static void Warning(this ILogger logger, string message)
        => logger.Log(new LogMessage(message, LogLevel.Warning));

    public static void Error(this ILogger logger, string message) 
        => logger.Log(new LogMessage(message, LogLevel.Error));
}