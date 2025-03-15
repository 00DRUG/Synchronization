using Synchronization.Models;

namespace Synchronization.Interfaces;

public interface ILogger
{
    void Log(LogMessage log);
}