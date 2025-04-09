using System.Collections.Generic;
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
                var filteredhourlyPollutants = finalhourlypollutantresult.Where(p =>
                                                        (p.Pollutantname == "Nitrogen dioxide" && Convert.ToDouble(p.Value) > 200.5) || //200
                                                        (p.Pollutantname == "Sulphur dioxide" && Convert.ToDouble(p.Value) > 350.5))  //350
                                                        .GroupBy(p => p.Pollutantname)
                                                        .Select(g => new { PollutantName = g.Key, Count = g.Count() })
                                                        .ToList();

                var hourlyexceedances = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" }
                    .Select(name => new
                    {
                        PollutantName = name,
                        HourlyexceedancesCount = filteredhourlyPollutants.FirstOrDefault(p => p.PollutantName == name)?.Count.ToString() ?? (name == "Nitrogen dioxide" || name == "Sulphur dioxide" ? "0" : "n/a")
                    }).ToList();

                var Daily_Average = finalhourlypollutantresult
                                                .GroupBy(x => new
                                                {
                                                    ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(),
                                                    x.Pollutantname,
                                                    x.Verification
                                                })
                                                .Select(x => new
                                                {
                                                    x.Key.ReportDate,
                                                    x.Key.Pollutantname,
                                                    x.Key.Verification,
                                                    Values = x.Where(y => y.Value != "-99").Select(y => Convert.ToDecimal(y.Value)).ToList(),
                                                    Count = x.Count()
                                                })
                                                .Select(x => new Finaldata
                                                {
                                                    ReportDate = x.ReportDate,
                                                    DailyPollutantname = x.Pollutantname,
                                                    DailyVerification = x.Verification,
                                                    Total = (x.Values.Count() >= 0.75 * x.Count) ? x.Values.Average() : 0
                                                }).ToList();
                //To get the number of daily exceedances for a selected year and selected monitoring station
                var filtereddailyPollutants = Daily_Average.Where(p =>
                                                        (p.DailyPollutantname == "PM10" && Convert.ToDouble(p.Total) > 50.5) || //200
                                                        (p.DailyPollutantname == "Sulphur dioxide" && Convert.ToDouble(p.Total) > 125.5))  //350
                                                        .GroupBy(p => p.DailyPollutantname)
                                                        .Select(g => new { DailyPollutantname = g.Key, Count = g.Count() })
                                                        .ToList();

                var dailyexceedances = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" }
                    .Select(name => new
                    {
                        PollutantName = name,
                        dailyexceedancesCount = filtereddailyPollutants.FirstOrDefault(p => p.DailyPollutantname == name)?.Count.ToString() ?? (name == "PM10" || name == "Sulphur dioxide" ? "0" : "n/a")
                    }).ToList();

                var dataVerifiedTag = finalhourlypollutantresult
                                        .Select((pollutant, index) => new { Pollutant = pollutant, Index = index })
                                        .Where(x => x.Pollutant.Verification == "2")
                                        .Select(x => x.Index > 0 ? $"Data has been verified until {Convert.ToDateTime(finalhourlypollutantresult[x.Index - 1].StartTime).ToString("dd MMMM")}" : "Data has not been verified")
                                        .FirstOrDefault() ?? "Data has been verified";

                var mergedexceedances = hourlyexceedances.Join(dailyexceedances,
                                        h => h.PollutantName,
                                        d => d.PollutantName,
                                        (h, d) => new
                                        {
                                            PollutantName = h.PollutantName,
                                            hourlyCount = h.HourlyexceedancesCount,
                                            dailyCount = d.dailyexceedancesCount,
                                            dataVerifiedTag = dataVerifiedTag
                                        }).ToList();

                return mergedexceedances;
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
