using System;
using System.IO;
using System.Threading;
using System.Linq;
using Moq;
using Xunit;
using Synchronization.Services;
using Synchronization.Models;
using Synchronization.Enums;

public class LoggerTests : IDisposable
{
    private readonly string _testLogFile = "test_log.txt";
    private readonly Logger _logger;
    public LoggerTests()
    {
        if (File.Exists(_testLogFile))
            File.Delete(_testLogFile);

        _logger = new Logger(_testLogFile);
    }

    [Fact]
    public void Log_ShouldCallWriteLineInStreamWriter()
    {
        // Arrange
        var mockStreamWriter = new Mock<StreamWriter>(new MemoryStream());
        var logger = new Logger("mock_log.txt");
        logger.Dispose();
        typeof(Logger)
            .GetField("_streamWriter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(logger, mockStreamWriter.Object);

        var logMessage = new LogMessage("Mock log", LogLevel.Info);

        // Act
        logger.Log(logMessage);
        logger.StopLogging();

        // Assert
        mockStreamWriter.Verify(x => x.WriteLine(It.Is<string>(s => s.Contains("Mock log"))), Times.Once);
    }
    [Fact]
    public void Log_ShouldWriteCorrectFormat()
    {
        // Arrange
        var logMessage = new LogMessage("Test log entry", LogLevel.Warning, OperationType.Delete);

        // Act
        _logger.Log(logMessage);
        _logger.StopLogging();

        // Assert
        string logContent = File.ReadAllText(_testLogFile);
        Assert.Contains("Test log entry", logContent);
        Assert.Contains("Warning", logContent);
        Assert.Contains("Delete", logContent);
    }

    [Fact]
    public void Log_MultipleEntries_ShouldBeOrdered()
    {
        // Arrange
        var log1 = new LogMessage("First log", LogLevel.Info);
        var log2 = new LogMessage("Second log", LogLevel.Error);
        var log3 = new LogMessage("Third log", LogLevel.Debug);

        // Act
        _logger.Log(log1);
        _logger.Log(log2);
        _logger.Log(log3);
        _logger.StopLogging();

        // Assert
        string[] lines = File.ReadAllLines(_testLogFile);
        Assert.Equal(3, lines.Length);
        Assert.Contains("First log", lines[0]);
        Assert.Contains("Second log", lines[1]);
        Assert.Contains("Third log", lines[2]);
    }

    [Fact]
    public void Log_ShouldHandleMultipleThreadsSafely()
    {
        // Arrange
        int threadCount = 5;
        Thread[] threads = new Thread[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                _logger.Log(new LogMessage($"Thread {index} message", LogLevel.Info));
            });
            threads[i].Start();
        }

        foreach (var thread in threads)
            thread.Join();

        _logger.StopLogging();

        // Assert
        string logContent = File.ReadAllText(_testLogFile);
        for (int i = 0; i < threadCount; i++)
        {
            Assert.Contains($"Thread {i} message", logContent);
        }
    }
    [Fact]
    public void Log_MultipleEntries_ShouldAppearInFile()
    {
        // Arrange
        _logger.Log(new LogMessage("First entry", LogLevel.Warning));
        _logger.Log(new LogMessage("Second entry", LogLevel.Error));

        // Act
        _logger.StopLogging();

        // Assert
        string logContent = File.ReadAllText(_testLogFile);
        Assert.Contains("First entry", logContent);
        Assert.Contains("Second entry", logContent);
    }
    [Fact]
    public void Log_AfterDispose_ShouldThrowException()
    {
        // Arrange
        _logger.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _logger.Log(new LogMessage("Should not be logged", LogLevel.Info)));
    }

    

    public void Dispose()
    {
        _logger.Dispose();
        if (File.Exists(_testLogFile))
            File.Delete(_testLogFile);
    }
}
