﻿using System.Collections.Concurrent;
using 
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

        // Start logging by initializing the StreamWriter and starting the logging thread
        public void StartLogging()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(Logger));
            _streamWriter = new StreamWriter(_logFilePath, append: true);
            _isRunning = true;

            // Start the background thread for processing log messages
            _loggingThread = new Thread(BackgroundLogProcessing);
            _loggingThread.Start();
        }

        // Background thread to process the log queue and write logs
        private void BackgroundLogProcessing()
        {
            while (_isRunning)
            {
                try
                {
                    var logMessage = _logQueue.Take(); 

                    _streamWriter.WriteLine(logMessage);
                    _streamWriter.Flush();
                }
                catch (InvalidOperationException)
                {
                    // The BlockingCollection was marked complete for adding new items
                    break;
                }
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

        // Method to add a log message to the queue
        public void Log(LogMessage logMessage)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(Logger));
            _logQueue.Add(logMessage);
        }

        // specific log levels
        public void LogAdd(string filePath, string message) =>
       Log(new LogMessage(message, LogLevel.Info, OperationType.Add));

        public void LogUpdate(string filePath, string message) =>
            Log(new LogMessage(message, LogLevel.Info, OperationType.Update));

        public void LogDelete(string filePath, string message) =>
            Log(new LogMessage(message, LogLevel.Info, OperationType.Delete));

        public void LogSyncStart(string message) =>
            Log(new LogMessage(message, LogLevel.Info, OperationType.SyncStart));

        public void LogSyncEnd(string message) =>
            Log(new LogMessage(message, LogLevel.Info, OperationType.SyncEnd));
        public void Info(string message) => Log(new LogMessage(message, LogLevel.Info));
        public void Warning(string message) => Log(new LogMessage(message, LogLevel.Warning));
        public void Error(string message) => Log(new LogMessage(message, LogLevel.Error));
    }
