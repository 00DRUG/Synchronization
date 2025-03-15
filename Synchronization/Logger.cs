﻿using Synchronization.Interfaces;
using Synchronization.Models;
using System.Collections.Concurrent;
namespace Synchronization;



public class Logger : ILogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly BlockingCollection<LogMessage> _logQueue;
    private StreamWriter _streamWriter;
    private Thread _loggingThread;
    private bool _isRunning;
    private bool _isDisposed;

    public Logger(string logFilePath = "log.txt")
    {
        _logFilePath = logFilePath;
        _logQueue = new BlockingCollection<LogMessage>(new ConcurrentQueue<LogMessage>());
        _isRunning = false;
        _isDisposed = false;

        // Ensure the directory exists before starting logging
        if (string.IsNullOrEmpty(_logFilePath))
            throw new ArgumentException("Log file path cannot be null or empty");

        var directoryPath = Path.GetDirectoryName(_logFilePath);
        if (directoryPath != null && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Ensure the log file exists
        if (!File.Exists(_logFilePath))
        {
            using (File.Create(_logFilePath)) { }
        }
    }
    public void Log(LogMessage logMessage)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(Logger));

        _logQueue.Add(logMessage);
    }

    // Start logging by initializing the StreamWriter and starting the logging thread
    public void StartLogging()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(Logger));

        _isRunning = true;
        _loggingThread = new Thread(BackgroundLogProcessing);
        _loggingThread.Start();
    }
    // Stop logging and close resources
    public void StopLogging()
    {
        if (_isRunning)
        {
            _isRunning = false;
            _logQueue.CompleteAdding();
            _loggingThread?.Join();
        }
    }
    // Background thread to process the log queue and write logs
    private void BackgroundLogProcessing()
    {
        using (var _streamWriter = new StreamWriter(_logFilePath, append: true))
            foreach ( var logMessage in _logQueue.GetConsumingEnumerable())
            {

                _streamWriter.WriteLine(logMessage);
                _streamWriter.Flush();
            }
    }

    // Dispose resources
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        StopLogging();
        _streamWriter?.Dispose();
        _logQueue.Dispose();
    }
}
