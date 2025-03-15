

using Synchronization.Enums;
using Synchronization.Models;
using Synchronization.Interfaces;
namespace Synchronization.Extensions;
public static class ILoggerExtensions
{
    public void LogAdd(this ILogger logger, string message) =>
        logger.Log(new LogMessage(message, LogLevel.Info, OperationType.Add));

    public void LogUpdate(this ILogger logger, string message) =>
        logger.Log(new LogMessage(message, LogLevel.Info, OperationType.Update));

    public void LogDelete(this ILogger logger, string message) =>
        logger.Log(new LogMessage(message, LogLevel.Info, OperationType.Delete));

    public void LogSyncStart(this ILogger logger, string message) =>
        logger.Log(new LogMessage(message, LogLevel.Info, OperationType.SyncStart));

    public void LogSyncEnd(this ILogger logger,string message) =>
        logger.Log(new LogMessage(message, LogLevel.Info, OperationType.SyncEnd));
    public void Info(this ILogger logger,string message) => logger.Log(new LogMessage(message, LogLevel.Info));
    public void Warning(this ILogger logger,string message) => logger.Log(new LogMessage(message, LogLevel.Warning));
    public void Error(this ILogger logger,string message) => logger.Log(new LogMessage(message, LogLevel.Error));
}