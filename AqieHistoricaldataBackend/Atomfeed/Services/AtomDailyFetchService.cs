using System.Collections.Generic;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDailyFetchService(ILogger<AtomDailyFetchService> logger) : IAtomDailyFetchService
    {
        public async Task<List<FinalData>> GetAtomDailydatafetch(List<FinalData> finalhourlypollutantresult, QueryStringData data)
        {
            try
            {
                var Daily_Average = finalhourlypollutantresult
                                                .GroupBy(x => new
                                                {
                                                    ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(),
                                                    x.PollutantName,
                                                    x.Verification
                                                })
                                                .Select(x => new
                                                {
                                                    x.Key.ReportDate,
                                                    x.Key.PollutantName,
                                                    x.Key.Verification,
                                                    Values = x.Where(y => y.Value != "-99").Select(y => Convert.ToDecimal(y.Value)).ToList(),
                                                    Count = x.Count()
                                                })
                                                .Select(x => new FinalData
                                                {
                                                    ReportDate = x.ReportDate,
                                                    DailyPollutantName = x.PollutantName,
                                                    DailyVerification = x.Verification,
                                                    Total = (x.Values.Count() >= 0.75 * x.Count) ? x.Values.Average() : 0
                                                }).ToList();
                return Daily_Average;
            }
            catch (Exception ex)
            {
                logger.LogError("Error in Atom daily feed fetch {Error}", ex.Message);
                logger.LogError("Error in Atom daily feed fetch {Error}", ex.StackTrace);
                List<FinalData> Final_list = new List<FinalData>();
                return Final_list;
            }
        }
    }
}
