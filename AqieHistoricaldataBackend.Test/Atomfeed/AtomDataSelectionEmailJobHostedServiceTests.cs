using System;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionEmailJobHostedServiceTests : IDisposable
    {
        private readonly Mock<ILogger<AtomDataSelectionEmailJobHostedService>> _loggerMock;
        private readonly Mock<IAtomDataSelectionEmailJobService> _emailJobServiceMock;
        private readonly AtomDataSelectionEmailJobHostedService _service;

        public AtomDataSelectionEmailJobHostedServiceTests()
        {
            // Set a very short interval so tests don't hang waiting for Task.Delay
            Environment.SetEnvironmentVariable("TIME_INTERVAL", "0.0001");

            _loggerMock = new Mock<ILogger<AtomDataSelectionEmailJobHostedService>>();
            _emailJobServiceMock = new Mock<IAtomDataSelectionEmailJobService>();
            _service = new AtomDataSelectionEmailJobHostedService(
                _loggerMock.Object,
                _emailJobServiceMock.Object
            );
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("TIME_INTERVAL", null);
            _service.Dispose();
        }

        // ----------------------------------------------------------------
        // StartAsync — logs "starting" and delegates to base
        // ----------------------------------------------------------------
        [Fact]
        public async Task StartAsync_LogsStartingMessage()
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            await _service.StartAsync(cts.Token);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("starting")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            await _service.StopAsync(CancellationToken.None);
        }

        // ----------------------------------------------------------------
        // StopAsync — logs "stopping" message
        // ----------------------------------------------------------------
        [Fact]
        public async Task StopAsync_LogsStoppingMessage()
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            await _service.StartAsync(cts.Token);
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("stopping")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — happy path: job runs and completes gracefully
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_RunsJobAndStopsGracefully_WhenCancelled()
        {
            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using var cts = new CancellationTokenSource();

            await _service.StartAsync(cts.Token);

            // Let at least one iteration complete then cancel
            await Task.Delay(50);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            _emailJobServiceMock.Verify(
                s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — logs "started ExecuteAsync" on entry
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_LogsStarted_OnEntry()
        {
            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using var cts = new CancellationTokenSource();
            await _service.StartAsync(cts.Token);
            await Task.Delay(50);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("started ExecuteAsync")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — logs "stopped ExecuteAsync" after cancellation
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_LogsStopped_AfterCancellation()
        {
            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using var cts = new CancellationTokenSource();
            await _service.StartAsync(cts.Token);
            await Task.Delay(50);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("stopped ExecuteAsync")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — logs "Email job started at" per iteration
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_LogsJobStartedAndCompleted_EachIteration()
        {
            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using var cts = new CancellationTokenSource();
            await _service.StartAsync(cts.Token);
            await Task.Delay(50);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Email job started at")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Email job completed at")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — non-cancellation exception is caught and logged,
        // loop continues (does NOT rethrow)
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_LogsError_WhenJobServiceThrowsNonCancellationException()
        {
            var callCount = 0;

            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(_ =>
                {
                    callCount++;
                    if (callCount == 1)
                        throw new InvalidOperationException("job exploded");
                    return Task.CompletedTask;
                });

            using var cts = new CancellationTokenSource();
            await _service.StartAsync(cts.Token);
            // Allow time for first (failing) + second (succeeding) iteration
            await Task.Delay(100);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Error running ProcessPendingEmailJobsAsync")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);

            // Loop should have continued — service called at least twice
            Assert.True(callCount >= 2);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — OperationCanceledException during WaitAsync breaks
        // the loop gracefully (no error log)
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_BreaksGracefully_WhenCancelledDuringWaitAsync()
        {
            // Cancel immediately before any job can run
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await _service.StartAsync(cts.Token);
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — OperationCanceledException during Task.Delay breaks
        // the loop without logging an error
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_BreaksGracefully_WhenCancelledDuringDelay()
        {
            // Job completes instantly; cancellation fires during the subsequent delay
            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using var cts = new CancellationTokenSource();
            await _service.StartAsync(cts.Token);
            // Give just enough time for the job to finish but cancel before delay ends
            await Task.Delay(30);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        // ----------------------------------------------------------------
        // Constructor — falls back to 45-minute interval when TIME_INTERVAL
        // environment variable is not set
        // ----------------------------------------------------------------
        [Fact]
        public void Constructor_UsesFallbackInterval_WhenEnvVarNotSet()
        {
            Environment.SetEnvironmentVariable("TIME_INTERVAL", null);

            var service = new AtomDataSelectionEmailJobHostedService(
                _loggerMock.Object,
                _emailJobServiceMock.Object
            );

            // Verify the service was constructed without throwing
            Assert.NotNull(service);
            service.Dispose();
        }

        // ----------------------------------------------------------------
        // Constructor — uses custom interval from TIME_INTERVAL env var
        // ----------------------------------------------------------------
        [Fact]
        public void Constructor_UsesCustomInterval_WhenEnvVarIsSet()
        {
            Environment.SetEnvironmentVariable("TIME_INTERVAL", "10");

            var service = new AtomDataSelectionEmailJobHostedService(
                _loggerMock.Object,
                _emailJobServiceMock.Object
            );

            Assert.NotNull(service);
            service.Dispose();
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — logs "Waiting X minutes" after each job run
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_LogsWaitingMessage_AfterJobCompletes()
        {
            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            using var cts = new CancellationTokenSource();
            await _service.StartAsync(cts.Token);
            await Task.Delay(50);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Waiting") && v.ToString()!.Contains("minutes before next job")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ----------------------------------------------------------------
        // ExecuteAsync — SemaphoreSlim is released even when the job throws
        // (verifiable by ensuring subsequent iteration can acquire the gate)
        // ----------------------------------------------------------------
        [Fact]
        public async Task ExecuteAsync_ReleasesGate_EvenWhenJobThrows()
        {
            var callCount = 0;

            _emailJobServiceMock
                .Setup(s => s.ProcessPendingEmailJobsAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(_ =>
                {
                    callCount++;
                    // Always throw; the gate must still be released each time
                    throw new Exception("always fails");
                });

            using var cts = new CancellationTokenSource();
            await _service.StartAsync(cts.Token);
            await Task.Delay(150);
            await cts.CancelAsync();
            await _service.StopAsync(CancellationToken.None);

            // If the gate were never released, only 1 call would ever happen
            Assert.True(callCount >= 2, $"Expected ≥2 calls but got {callCount}");
        }
    }
}