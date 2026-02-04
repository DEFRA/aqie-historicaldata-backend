using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionEmailJobHostedService : BackgroundService
    {
        private readonly ILogger<AtomDataSelectionEmailJobHostedService> _logger;
        private readonly IAtomDataSelectionEmailJobService _emailJobService;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

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
                    await _emailJobService.ProcessPendingEmailJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running ProcessPendingEmailJobsAsync.");
                }
                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}