﻿
using Synchronization.Models;
namespace Synchronization.Interfaces;

public interface ISynchronizer
{
    Task StartAsync(InputParameters input, CancellationTokenSource cancellationTokenSource);
}

