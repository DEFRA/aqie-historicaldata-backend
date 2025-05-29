using System.Collections.Generic;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class HistoryexceedenceService(ILogger<HistoryexceedenceService> logger, IHttpClientFactory httpClientFactory,
        IAtomHourlyFetchService atomHourlyFetchService, IAtomDailyFetchService AtomDailyFetchService, 
        IAtomAnnualFetchService AtomAnnualFetchService) : IHistoryexceedenceService
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

                var customOrder = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" };
                var distinctpollutant = finalhourlypollutantresult.Select(S => S.Pollutantname).Distinct().OrderBy(m => customOrder.IndexOf(m)).ToList();

                var hourlyexceedances = distinctpollutant
                    .Select(name => new
                    {
                        PollutantName = name,
                        HourlyexceedancesCount = filteredhourlyPollutants.FirstOrDefault(p => p.PollutantName == name)?.Count.ToString() ?? (name == "Nitrogen dioxide" || name == "Sulphur dioxide" ? "0" : "n/a")
                    }).ToList();

                var dailyAverage = await AtomDailyFetchService.GetAtomDailydatafetch(finalhourlypollutantresult, data);

                var filtereddailyPollutants = dailyAverage.Where(p =>
                                                        (p.DailyPollutantname == "PM10" && Convert.ToDouble(p.Total) > 50.5) || //200
                                                        (p.DailyPollutantname == "Sulphur dioxide" && Convert.ToDouble(p.Total) > 125.5))  //350
                                                        .GroupBy(p => p.DailyPollutantname)
                                                        .Select(g => new { DailyPollutantname = g.Key, Count = g.Count() })
                                                        .ToList();

                var dailyexceedances = distinctpollutant
                    .Select(name => new
                    {
                        PollutantName = name,
                        dailyexceedancesCount = filtereddailyPollutants.FirstOrDefault(p => p.DailyPollutantname == name)?.Count.ToString() ?? (name == "PM10" || name == "Sulphur dioxide" ? "0" : "n/a")
                    }).ToList();

                //var annualexceedances = await AtomAnnualFetchService.GetAtomAnnualdatafetch(finalhourlypollutantresult, data);
                var annualexceedances = dailyAverage.GroupBy(x => new
                                                {
                                                    ReportDate = Convert.ToDateTime(x.ReportDate).Year.ToString(),
                                                    x.DailyPollutantname
                                                })
                                             .Select(x =>
                                             {
                                                 var validTotals = x.Where(y => Convert.ToDecimal(y.Total) != 0).ToList();
                                                 return new Finaldata
                                                 {
                                                     ReportDate = x.Key.ReportDate,
                                                     AnnualPollutantname = x.Key.DailyPollutantname,
                                                     Total = validTotals.Any() ? Math.Round(validTotals.Average(y => Convert.ToDecimal(y.Total))) : '-'
                                                 };
                                             }).ToList();

                var dataVerifiedTag = finalhourlypollutantresult
                                        .Select((pollutant, index) => new { Pollutant = pollutant, Index = index })
                                        .Where(x => x.Pollutant.Verification == "2")
                                        .Select(x => x.Index > 0 ? $"Data has been verified until {Convert.ToDateTime(finalhourlypollutantresult[x.Index - 1].StartTime).ToString("dd MMMM")}" : "Data has not been verified")
                                        .FirstOrDefault() ?? "Data has been verified";

                // Total possible data points (assuming hourly data collection for one day)
                int daysinYear = DateTime.IsLeapYear(Convert.ToInt32(year)) ? 366 : 365;
                int totalPossibleDataPoints = daysinYear * 24;

                //var data_pm10 = finalhourlypollutantresult.Where(p => p.Pollutantname == "PM10" && Convert.ToInt32(p.Validity) > 0)
                //                                          .GroupBy(p => p.Pollutantname)
                //                                          .Select(g => new { Pollutantname = g.Key, Count = g.Select(p => p.StartTime).Distinct().Count() }).ToList();

                var dataCapturePercentages = finalhourlypollutantresult
                .Where(p => Convert.ToInt32(p.Validity) > 0) // Filter valid data points
                           .GroupBy(p => p.Pollutantname) // Group by pollutant name
                           .Select(g => new
                           {
                               PollutantName = g.Key,
                               //DataCapturePercentage = ((double)g.Count() / totalPossibleDataPoints) * 100// Calculate Data Capture Percentage
                               DataCapturePercentage = ((double)g.Select(p => p.StartTime).Distinct().Count() / totalPossibleDataPoints) * 100// Calculate Data Capture Percentage
                           }).ToList();

                var mergedexceedances = (from hourly in hourlyexceedances
                            join daily in dailyexceedances on hourly.PollutantName equals daily.PollutantName
                            join Percentage in dataCapturePercentages on daily.PollutantName equals Percentage.PollutantName
                            join annual in annualexceedances on Percentage.PollutantName equals annual.AnnualPollutantname
                            select new
                            {
                                PollutantName = hourly.PollutantName,
                                hourlyCount = hourly.HourlyexceedancesCount,
                                dailyCount = daily.dailyexceedancesCount,
                                annualcount = annual.Total + " µg/m3",
                                dataVerifiedTag = dataVerifiedTag,
                                dataCapturePercentage = Math.Round(Percentage.DataCapturePercentage) +"%"
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
