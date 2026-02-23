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
                //To get the daily average
                var annualAverage = dailyAverage
                    .Where(x => x.ReportDate != null && DateTime.TryParse(x.ReportDate, out _))
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
                                            Total = validTotals.Any() ? validTotals.Average(y => Convert.ToDecimal(y.Total)) : 0
                                        };
                                    }).ToList();
                return annualAverage;
            }
            catch (Exception ex)
            {
                logger.LogError("Error in Atom Annual feed fetch {Error}", ex.Message);
                logger.LogError("Error in Atom Annual feed fetch {Error}", ex.StackTrace);
                List<FinalData> Final_list = new List<FinalData>();
                return Final_list;
            }
        }
    }
}