using System.Globalization;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomAnnualFetchService(ILogger<AtomAnnualFetchService> logger,
         IAtomDailyFetchService AtomDailyFetchService) : IAtomAnnualFetchService
    {
        public async Task<List<FinalData>> GetAtomAnnualdatafetch(List<FinalData> finalhourlypollutantresult, QueryStringData data)
        {
            try
            {
                var dailyAverage = await AtomDailyFetchService.GetAtomDailydatafetch(finalhourlypollutantresult, data);
                var annualAverage = dailyAverage
                    .Where(x => x.ReportDate != null && DateTime.TryParse(x.ReportDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    .GroupBy(x => new
                    {
                        ReportDate = Convert.ToDateTime(x.ReportDate).Year.ToString(),
                        x.DailyPollutantName,
                        x.DailyVerification
                    })
                    .Select(x =>
                    {
                        var validTotals = x.Where(y => Convert.ToDecimal(y.Total) != 0).ToList();
                        return new FinalData
                        {
                            ReportDate = x.Key.ReportDate,
                            AnnualPollutantName = x.Key.DailyPollutantName,
                            AnnualVerification = x.Key.DailyVerification,
                            Total = validTotals.Count > 0 ? validTotals.Average(y => Convert.ToDecimal(y.Total)) : 0
                        };
                    }).ToList();
                return annualAverage;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Atom Annual feed fetch {Error}", ex.Message);
                return new List<FinalData>();
            }
        }
    }
}