using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AnnualAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger) : IAnnualAtomFeedExportCSV
    {
        public async Task<byte[]> annualatomfeedexport_csv(List<FinalData> Final_list, QueryStringData data)
        {
            try
            {
                var groupedData = GroupFinalData(Final_list);
                var distinctPollutants = Final_list.Select(s => s.AnnualPollutantName).Distinct().OrderBy(m => m).ToList();

                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream);

                //using var writer = new StreamWriter("AnnualPivotData.csv");

                WriteCsvHeader(writer, data);
                WritePollutantHeaders(writer, distinctPollutants);
                WriteGroupedData(writer, groupedData, distinctPollutants);

                writer.Flush();
                return memoryStream.ToArray();
                // Uncomment for local csv write
                // byte[] byteArray = [];
                // return byteArray;
            }
            catch (Exception ex)
            {
                logger.LogError("Annual download CSV error: {Error}", ex.Message);
                logger.LogError("Stacktrace: {Error}", ex.StackTrace);
                return new byte[] { 0x20 };
            }
        }
        private void WriteCsvHeader(StreamWriter writer, QueryStringData data)
        {
            string stationfetchdate = Convert.ToDateTime(data.StationReadDate).ToString();
            writer.WriteLine($"Annual Average data from Defra on {stationfetchdate}");
            writer.WriteLine($"Site Name,{data.SiteName}");
            writer.WriteLine($"Site Type,{data.SiteType}");
            writer.WriteLine($"Region,{data.Region}");
            writer.WriteLine($"Latitude,{data.Latitude}");
            writer.WriteLine($"Longitude,{data.Longitude}");
            writer.WriteLine("Notes:,[1] All Data GMT hour ending;  [2] Some shorthand is used V = Verified P = Provisionally Verified N = Not Verified S = Suspect [3] Unit of measurement (for pollutants) = ugm-3");
        }
        private void WritePollutantHeaders(StreamWriter writer, List<string> pollutants)
        {
            writer.Write("Date");
            foreach (var pollutant in pollutants)
            {
                string header = GetPollutantHeader(pollutant);
                writer.Write($",{header},Status");
            }
            writer.WriteLine();
        }
        private void WriteGroupedData(StreamWriter writer, List<PivotPollutant> groupedData, List<string> pollutants)
        {
            foreach (var item in groupedData)
            {
                int year = Convert.ToInt32(item.Date);
                DateTime startDate = new DateTime(year, 1, 1);
                DateTime endDate = (year == DateTime.Now.Year) ? DateTime.Now.Date : new DateTime(year, 12, 31);
                string dateRange = $"{startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}";

                writer.Write(dateRange);

                foreach (var pollutant in pollutants)
                {
                    var sub = item.SubPollutant.FirstOrDefault(s => s.PollutantName == pollutant);
                    writer.Write($",{sub?.PollutantValue ?? ""},{sub?.Verification ?? ""}");
                }
                writer.WriteLine();
            }
        }
        private string GetPollutantHeader(string pollutant) => pollutant switch
        {
            "PM10" => "PM10 particulate matter (Hourly measured)",
            "PM2.5" => "PM2.5 particulate matter (Hourly measured)",
            _ => pollutant
        };
        private List<PivotPollutant> GroupFinalData(List<FinalData> finalList)
        {
            return finalList.GroupBy(x => x.ReportDate)
                .Select(y => new PivotPollutant
                {
                    Date = y.Key,
                    SubPollutant = y.Select(x => new SubPollutantItem
                    {
                        PollutantName = x.AnnualPollutantName,
                        PollutantValue = x.Total == 0 ? "no data" : x.Total.ToString(),
                        Verification = MapVerification(x.AnnualVerification)
                    }).ToList()
                }).ToList();
        }
        private string MapVerification(string code) => code switch
        {
            "1" => "V",
            "2" => "P",
            "3" => "N",
            _ => "others"
        };
    }

}
