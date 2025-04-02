using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class HistoryexceedenceService(ILogger<HistoryexceedenceService> logger, IHttpClientFactory httpClientFactory,
        IAtomHourlyFetchService atomHourlyFetchService) : IHistoryexceedenceService
    {
        public async Task<dynamic> GetHistoryexceedencedata(querystringdata data)
        {
            try
            {
                string siteId = data.siteId;
                string year = data.year;
                string downloadfilter = "All";

                var finalhourlypollutantresult = await atomHourlyFetchService.GetAtomHourlydatafetch(siteId, year, downloadfilter);
                //To get the number of hourly exceedances for a selected year and selected monitoring station
                var filteredPollutants = finalhourlypollutantresult.Where(p =>
                                                        (p.Pollutantname == "Nitrogen dioxide" && Convert.ToDouble(p.Value) > 200.5) || //200
                                                        (p.Pollutantname == "Sulphur dioxide" && Convert.ToDouble(p.Value) > 350.5))  //350
                                                        .GroupBy(p => p.Pollutantname)
                                                        .Select(g => new { PollutantName = g.Key, Count = g.Count() })
                                                        .ToList();

                var hourlyexceedances = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" }
                    .Select(name => new
                    {
                        PollutantName = name,
                        HourlyexceedancesCount = filteredPollutants.FirstOrDefault(p => p.PollutantName == name)?.Count.ToString() ?? (name == "Nitrogen dioxide" || name == "Sulphur dioxide" ? "0" : "n/a")
                    }).ToList();
                return hourlyexceedances;
            }
            catch (Exception ex)
            {
                logger.LogError("Error in Atom Historyexceedencedata {Error}", ex.Message);
                logger.LogError("Error in Atom Historyexceedencedata {Error}", ex.StackTrace);
                return "Failure";
            }

        }
    }
}
