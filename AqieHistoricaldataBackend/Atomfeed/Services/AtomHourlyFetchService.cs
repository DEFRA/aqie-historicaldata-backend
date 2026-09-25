using Newtonsoft.Json.Linq;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Text.RegularExpressions;


namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHourlyFetchService(
        ILogger<AtomHourlyFetchService> logger,
        IHttpClientFactory httpClientFactory)
        : AtomFeedFetchServiceBase(httpClientFactory), IAtomHourlyFetchService
    {
        protected override ILogger Logger => logger;

        public async Task<List<FinalData>> GetAtomHourlydatafetch(string siteID, string year, string downloadfilter)
            => (await GetAtomHourlydatafetchWithStatus(siteID, year, downloadfilter)).Rows;

        public async Task<AtomHourlyFetchOutcome> GetAtomHourlydatafetchWithStatus(
            string siteID, string year, string downloadfilter, string? dataSource = null)
        {
            var pollutantsToDisplay = GetPollutantsToDisplay(downloadfilter);
            var fetch = await FetchAtomFeedResultAsync(siteID, year, dataSource);
            return new AtomHourlyFetchOutcome(ProcessAtomData(fetch.Features, pollutantsToDisplay), fetch.UpstreamFailed);
        }

        private static List<PollutantDetails> GetPollutantsToDisplay(string filter)
        {
            var allPollutants = new List<PollutantDetails>
            {
                new() { PollutantName = "Nitrogen dioxide",  PollutantMasterUrl = "8"    },
                new() { PollutantName = "PM10",              PollutantMasterUrl = "5"    },
                new() { PollutantName = "PM2.5",             PollutantMasterUrl = "6001" },
                new() { PollutantName = "Ozone",             PollutantMasterUrl = "7"    },
                new() { PollutantName = "Sulphur dioxide",   PollutantMasterUrl = "1"    }
            };

            var filtered = allPollutants.Where(p => p.PollutantName == filter);
            return filtered.Any() ? filtered.ToList() : allPollutants;
        }
    }
}