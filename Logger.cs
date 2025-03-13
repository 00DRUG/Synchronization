using System.Collections.Concurrent;
using static Synchronization.Logger;

namespace Synchronization
{


    public class Logger
    {
        private readonly string _logFilePath;
        private readonly BlockingCollection<LogMessage> _logQueue;
        private readonly Thread _loggingThread;
        private readonly StreamWriter _streamWriter;
        private bool _isRunning;

        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Debug
        }

        public Logger(string logFilePath = "log.txt")
        {
            _logFilePath = logFilePath;
            _logQueue = new BlockingCollection<LogMessage>(new ConcurrentQueue<LogMessage>());
            _streamWriter = new StreamWriter(_logFilePath, append: true);
            _isRunning = true;

            // Start a background thread to process the log queue
            _loggingThread = new Thread(BackgroundLogProcessing);
            _loggingThread.Start();
        }

        private void BackgroundLogProcessing()
        {
            while (_isRunning)
            {
                try
                {
                    var logMessage = _logQueue.Take(); 

                    _streamWriter.WriteLine(logMessage.ToString());
                    _streamWriter.Flush();

                    Console.WriteLine(logMessage.ToString());// Print to console, can remove 
                }
                catch (InvalidOperationException)
                {
                    // BlockingCollection was marked complete for adding new items
                    break;
                }
            }
        }

        public void StopLogging()
        {
            _isRunning = false;
            _logQueue.CompleteAdding(); 
            _loggingThread.Join();
            _streamWriter.Close();
        }

        // Log methods
        public void Log(LogMessage logMessage)
        {
            _logQueue.Add(logMessage); 
        }

        public void Info(string message) => Log(new LogMessage(message, LogLevel.Info));
        public void Warning(string message) => Log(new LogMessage(message, LogLevel.Warning));
        public void Error(string message) => Log(new LogMessage(message, LogLevel.Error));
        public void Debug(string message) => Log(new LogMessage(message, LogLevel.Debug));
    }
    public class LogMessage
    {
        public DateTime Timestamp { get; }
        public string Message { get; }
        public LogLevel Level { get; }

        public LogMessage(string message, LogLevel level)
        {
            Timestamp = DateTime.Now;
            Message = message;
            Level = level;
        }

        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}";
        }
    }


}
