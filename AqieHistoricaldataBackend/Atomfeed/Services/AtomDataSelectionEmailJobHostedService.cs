using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionEmailJobHostedService : BackgroundService
    {
        private readonly ILogger<AtomDataSelectionEmailJobHostedService> _logger;
        private readonly IAtomDataSelectionEmailJobService _emailJobService;

        // Interval taken from environment variable (default = 45 minutes)
        private readonly TimeSpan _interval =
            TimeSpan.FromMinutes(double.Parse(Environment.GetEnvironmentVariable("TIME_INTERVAL") ?? "45"));

        // Ensures only one job runs at a time
        private readonly SemaphoreSlim _singleRunGate = new(1, 1);

        public AtomDataSelectionEmailJobHostedService(
            ILogger<AtomDataSelectionEmailJobHostedService> logger,
            IAtomDataSelectionEmailJobService emailJobService)
        {
            _logger = logger;
            _emailJobService = emailJobService;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Email job service starting...");
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Email job service stopping...");
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AtomDataSelectionEmailJobHostedService started ExecuteAsync.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Prevents overlapping job execution
                    await _singleRunGate.WaitAsync(stoppingToken);

                    try
                    {
                        _logger.LogInformation("Email job started at: {time}", DateTime.UtcNow);
                        await _emailJobService.ProcessPendingEmailJobsAsync(stoppingToken);
                        _logger.LogInformation("Email job completed at: {time}", DateTime.UtcNow);
                    }
                    finally
                    {
                        _singleRunGate.Release();
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running ProcessPendingEmailJobsAsync.");
                }

                // Wait for interval after job completes
                try
                {
                    _logger.LogInformation("Waiting {minutes} minutes before next job...", _interval.TotalMinutes);
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("AtomDataSelectionEmailJobHostedService stopped ExecuteAsync.");
        }
    }
}
