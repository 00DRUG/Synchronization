

namespace Synchronization.Interfaces;

public interface ISynchronizer
{
    Task StartAsync(InputParameters input, CancellationToken cancellationTokenSource);
}

