using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomAnnualFetchService(ILogger<AtomAnnualFetchService> logger,
         IAtomDailyFetchService AtomDailyFetchService) : IAtomAnnualFetchService
    {
        public async Task<List<Finaldata>> GetAtomAnnualdatafetch(List<Finaldata> finalhourlypollutantresult, querystringdata data)
        {
            try
            {
                var dailyAverage = await AtomDailyFetchService.GetAtomDailydatafetch(finalhourlypollutantresult, data);
                //To get the daily average
                var annualAverage = dailyAverage.GroupBy(x => new
                {
                    ReportDate = Convert.ToDateTime(x.ReportDate).Year.ToString(),
                    x.DailyPollutantname,
                    x.DailyVerification
                })
                                    .Select(x =>
                                    {
                                        var validTotals = x.Where(y => Convert.ToDecimal(y.Total) != 0).ToList();
                                        decimal averageTotal = validTotals.Any() ? validTotals.Average(y => Convert.ToDecimal(y.Total)) : 0;
                                        return new Finaldata
                                        {
                                            ReportDate = x.Key.ReportDate,
                                            AnnualPollutantname = x.Key.DailyPollutantname,
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
                List<Finaldata> Final_list = new List<Finaldata>();
                return Final_list;
            }
        }
    }
}
