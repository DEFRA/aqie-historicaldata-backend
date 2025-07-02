using System.Collections.Generic;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class HistoryexceedenceService(ILogger<HistoryexceedenceService> Logger,
        IAtomHourlyFetchService AtomHourlyFetchService, IAtomDailyFetchService AtomDailyFetchService) : IHistoryexceedenceService
    {
        public async Task<dynamic> GetHistoryexceedencedata(querystringdata data)
        {
            try
            {
                string siteId = data.siteId;
                string year = data.year;
                string downloadfilter = "All";

                var hourlyData = await AtomHourlyFetchService.GetAtomHourlydatafetch(siteId, year, downloadfilter);
                var distinctPollutants = GetOrderedDistinctPollutants(hourlyData);
                var filteredHourly = GetFilteredHourlyPollutants(hourlyData);
                var hourlyExceedances = GetHourlyExceedances(distinctPollutants, filteredHourly);

                var dailyData = await AtomDailyFetchService.GetAtomDailydatafetch(hourlyData, data);
                var filteredDaily = GetFilteredDailyPollutants(dailyData);
                var dailyExceedances = GetDailyExceedances(distinctPollutants, filteredDaily);

                var annualExceedances = GetAnnualExceedances(dailyData);
                var dataVerifiedTag = GetDataVerifiedTag(hourlyData);
                var dataCapturePercentages = GetDataCapturePercentages(hourlyData, year);

                var mergedExceedances = MergeExceedanceData(
                    hourlyExceedances,
                    dailyExceedances,
                    annualExceedances,
                    dataCapturePercentages,
                    dataVerifiedTag);

                return mergedExceedances;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Atom Historyexceedencedata {Error}", ex.Message);
                Logger.LogError("Error in Atom Historyexceedencedata {Error}", ex.StackTrace);
                return "Failure";
            }
        }

        private List<string> GetOrderedDistinctPollutants(List<Finaldata> data)
        {
            var customOrder = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" };
            return data.Select(s => s.Pollutantname).Distinct().OrderBy(m => customOrder.IndexOf(m)).ToList();
        }

        private List<dynamic> GetFilteredHourlyPollutants(List<Finaldata> data)
        {
            return data.Where(p =>
                    (p.Pollutantname == "Nitrogen dioxide" && Convert.ToDouble(p.Value) > 200.5) ||
                    (p.Pollutantname == "Sulphur dioxide" && Convert.ToDouble(p.Value) > 350.5))
                .GroupBy(p => p.Pollutantname)
                .Select(g => new { PollutantName = g.Key, Count = g.Count() })
                .ToList<dynamic>();
        }

        private List<dynamic> GetHourlyExceedances(List<string> pollutants, List<dynamic> filtered)
        {
            return pollutants.Select(name => new
            {
                PollutantName = name,
                HourlyexceedancesCount = filtered.FirstOrDefault(p => p.PollutantName == name)?.Count.ToString()
                    ?? (name == "Nitrogen dioxide" || name == "Sulphur dioxide" ? "0" : "n/a")
            }).ToList<dynamic>();
        }

        private List<dynamic> GetFilteredDailyPollutants(List<Finaldata> data)
        {
            return data.Where(p =>
                    (p.DailyPollutantname == "PM10" && Convert.ToDouble(p.Total) > 50.5) ||
                    (p.DailyPollutantname == "Sulphur dioxide" && Convert.ToDouble(p.Total) > 125.5))
                .GroupBy(p => p.DailyPollutantname)
                .Select(g => new { DailyPollutantname = g.Key, Count = g.Count() })
                .ToList<dynamic>();
        }

        private List<dynamic> GetDailyExceedances(List<string> pollutants, List<dynamic> filtered)
        {
            return pollutants.Select(name => new
            {
                PollutantName = name,
                dailyexceedancesCount = filtered.FirstOrDefault(p => p.DailyPollutantname == name)?.Count.ToString()
                    ?? (name == "PM10" || name == "Sulphur dioxide" ? "0" : "n/a")
            }).ToList<dynamic>();
        }

        private List<Finaldata> GetAnnualExceedances(List<Finaldata> data)
        {
            return data.GroupBy(x => new
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
        }

        private string GetDataVerifiedTag(List<Finaldata> data)
        {
            return data.Select((pollutant, index) => new { Pollutant = pollutant, Index = index })
                .Where(x => x.Pollutant.Verification == "2")
                .Select(x => x.Index > 0
                    ? $"Data has been verified until {Convert.ToDateTime(data[x.Index - 1].StartTime):dd MMMM}"
                    : "Data has not been verified")
                .FirstOrDefault() ?? "Data has been verified";
        }

        private List<dynamic> GetDataCapturePercentages(List<Finaldata> data, string year)
        {
            int currentYear = DateTime.Now.Year;
            int yearToCheck = Convert.ToInt32(year);
            int daysInYear = (yearToCheck == currentYear)
                ? (DateTime.Now - new DateTime(currentYear, 1, 1)).Days
                : (DateTime.IsLeapYear(yearToCheck) ? 366 : 365);
            int totalPossibleDataPoints = daysInYear * 24;

            return data.Where(p => Convert.ToInt32(p.Validity) > 0)
                .GroupBy(p => p.Pollutantname)
                .Select(g => new
                {
                    PollutantName = g.Key,
                    DataCapturePercentage = ((double)g.Select(p => p.StartTime).Distinct().Count() / totalPossibleDataPoints) * 100
                }).ToList<dynamic>();
        }

        private List<dynamic> MergeExceedanceData(
            List<dynamic> hourly,
            List<dynamic> daily,
            List<Finaldata> annual,
            List<dynamic> percentages,
            string verifiedTag)
        {
            return (from h in hourly
                    join d in daily on h.PollutantName equals d.PollutantName
                    join p in percentages on d.PollutantName equals p.PollutantName
                    join a in annual on p.PollutantName equals a.AnnualPollutantname
                    select new
                    {
                        PollutantName = h.PollutantName,
                        hourlyCount = h.HourlyexceedancesCount,
                        dailyCount = d.dailyexceedancesCount,
                        annualcount = p.DataCapturePercentage > 74 ? a.Total + " µg/m3" : "-",
                        dataVerifiedTag = verifiedTag,
                        dataCapturePercentage = Math.Round(p.DataCapturePercentage) + "%"
                    }).ToList<dynamic>();
        }
    }

}
