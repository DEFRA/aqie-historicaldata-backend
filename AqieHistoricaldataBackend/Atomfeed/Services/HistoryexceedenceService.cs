using System.Globalization;
using Serilog;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class HistoryexceedenceService(ILogger<HistoryexceedenceService> Logger,
        IAtomHourlyFetchService AtomHourlyFetchService, IAtomDailyFetchService AtomDailyFetchService) : IHistoryexceedenceService
    {
        public async Task<dynamic?> GetHistoryexceedencedata(QueryStringData data)
        {
            try
            {
                // L15/L16: null-coalesce nullable string? properties to non-nullable string
                string siteId = data.SiteId ?? string.Empty;
                string year = data.Year ?? string.Empty;
                string downloadfilter = "All";

                // L19: siteId and year are now guaranteed non-null
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
                Logger.LogError(ex, "Error in Atom Historyexceedencedata");
                return "Failure";
            }
        }

        // L48: marked static; L51: OfType<string>() removes nulls, fixing List<string?> mismatch and null IndexOf arg
        private static List<string> GetOrderedDistinctPollutants(List<FinalData> data)
        {
            var customOrder = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" };
            return data.Select(s => s.PollutantName).OfType<string>().Distinct().OrderBy(m => customOrder.IndexOf(m)).ToList();
        }

        // L54: marked static
        private static List<dynamic> GetFilteredHourlyPollutants(List<FinalData> data)
        {
            return data.Where(p =>
                    (p.PollutantName == "Nitrogen dioxide" && Convert.ToDouble(p.Value) > 200.5) ||
                    (p.PollutantName == "Sulphur dioxide" && Convert.ToDouble(p.Value) > 350.5))
                .GroupBy(p => p.PollutantName)
                .Select(g => new { PollutantName = g.Key, Count = g.Count() })
                .ToList<dynamic>();
        }

        // L64: marked static
        private static List<dynamic> GetHourlyExceedances(List<string> pollutants, List<dynamic> filtered)
        {
            return pollutants.Select(name => new
            {
                PollutantName = name,
                HourlyexceedancesCount = filtered.FirstOrDefault(p => p.PollutantName == name)?.Count.ToString()
                    ?? (name == "Nitrogen dioxide" || name == "Sulphur dioxide" ? "0" : "n/a")
            }).ToList<dynamic>();
        }

        // L74: marked static
        private static List<dynamic> GetFilteredDailyPollutants(List<FinalData> data)
        {
            return data.Where(p =>
                    (p.DailyPollutantName == "PM10" && Convert.ToDouble(p.Total) > 50.5) ||
                    (p.DailyPollutantName == "Sulphur dioxide" && Convert.ToDouble(p.Total) > 125.5))
                .GroupBy(p => p.DailyPollutantName)
                .Select(g => new { DailyPollutantname = g.Key, Count = g.Count() })
                .ToList<dynamic>();
        }

        // L84: marked static
        private static List<dynamic> GetDailyExceedances(List<string> pollutants, List<dynamic> filtered)
        {
            return pollutants.Select(name => new
            {
                PollutantName = name,
                dailyexceedancesCount = filtered.FirstOrDefault(p => p.DailyPollutantname == name)?.Count.ToString()
                    ?? (name == "PM10" || name == "Sulphur dioxide" ? "0" : "n/a")
            }).ToList<dynamic>();
        }

        // L94: marked static; L108: Count > 0 instead of Any()
        private static List<FinalData> GetAnnualExceedances(List<FinalData> data)
        {
            return data.GroupBy(x => new
            {
                ReportDate = Convert.ToDateTime(x.ReportDate).Year.ToString(),
                x.DailyPollutantName
            })
            .Select(x =>
            {
                var validTotals = x.Where(y => Convert.ToDecimal(y.Total) != 0).ToList();
                return new FinalData
                {
                    ReportDate = x.Key.ReportDate,
                    AnnualPollutantName = x.Key.DailyPollutantName,
                    Total = validTotals.Count > 0
                        ? Math.Round(validTotals.Average(y => Convert.ToDecimal(y.Total)))
                        : (decimal?)null
                };
            }).ToList();
        }

        // L113: marked static
        private static string GetDataVerifiedTag(List<FinalData> data)
        {
            return data.Select((pollutant, index) => new { Pollutant = pollutant, Index = index })
                .Where(x => x.Pollutant.Verification == "2")
                .Select(x => x.Index > 0
                    ? $"Data has been verified until {Convert.ToDateTime(data[x.Index - 1].StartTime):dd MMMM}"
                    : "Data has not been verified")
                .FirstOrDefault() ?? "Data has been verified";
        }

        // L123: marked static; L129/L132: DateTimeKind.Local added; L137: nested ternary extracted;
        // L143/L151: CultureInfo.InvariantCulture added; L151: null-forgiving operator for already-validated StartTime
        private static List<dynamic> GetDataCapturePercentages(List<FinalData> data, string year)
        {
            DateTime today = DateTime.Today;
            int currentYear = DateTime.Now.Year;
            int yearToCheck = Convert.ToInt32(year);

            DateTime startInclusive = new DateTime(yearToCheck, 1, 1, 0, 0, 0, DateTimeKind.Local);

            // L137: extracted nested ternary into independent statements
            DateTime endExclusive;
            int daysInWindow;
            if (yearToCheck == currentYear)
            {
                endExclusive = today;
                daysInWindow = (today - startInclusive).Days;
            }
            else
            {
                endExclusive = new DateTime(yearToCheck + 1, 1, 1, 0, 0, 0, DateTimeKind.Local);
                daysInWindow = DateTime.IsLeapYear(yearToCheck) ? 366 : 365;
            }

            int totalPossibleDataPoints = daysInWindow * 24;

            var result = data
                .Where(p => Convert.ToInt32(p.Validity) > 0
                            && DateTime.TryParse(p.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTimeDt)
                            && startTimeDt >= startInclusive
                            && startTimeDt < endExclusive)
                .GroupBy(p => p.PollutantName)
                .Select(g => new
                {
                    PollutantName = g.Key,
                    DataCapturePercentage =
                        ((double)g.Select(p => DateTime.Parse(p.StartTime!, CultureInfo.InvariantCulture)).Distinct().Count()
                            / totalPossibleDataPoints) * 100
                })
                .ToList<dynamic>();

            return result;
        }

        // L158: marked static
        private static List<dynamic> MergeExceedanceData(
            List<dynamic> hourly,
            List<dynamic> daily,
            List<FinalData> annual,
            List<dynamic> percentages,
            string verifiedTag)
        {
            return (from h in hourly
                    join d in daily on h.PollutantName equals d.PollutantName
                    join p in percentages on d.PollutantName equals p.PollutantName
                    join a in annual on p.PollutantName equals a.AnnualPollutantName
                    select new
                    {
                        PollutantName = h.PollutantName,
                        hourlyCount = h.HourlyexceedancesCount,
                        dailyCount = d.dailyexceedancesCount,
                        annualcount = p.DataCapturePercentage > 74 ? a.Total + "" : "-",
                        dataVerifiedTag = verifiedTag,
                        dataCapturePercentage = Math.Round(p.DataCapturePercentage)
                    }).ToList<dynamic>();
        }
    }
}
