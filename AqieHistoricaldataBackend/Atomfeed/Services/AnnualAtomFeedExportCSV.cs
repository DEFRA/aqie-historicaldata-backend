using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AnnualAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger, IHttpClientFactory httpClientFactory) : IAnnualAtomFeedExportCSV
    {
        public async Task<byte[]> annualatomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        {
            try
            {
                var groupedData = GroupFinalData(Final_list);
                var distinctPollutants = Final_list.Select(s => s.AnnualPollutantname).Distinct().OrderBy(m => m).ToList();

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

        private void WriteCsvHeader(StreamWriter writer, querystringdata data)
        {
            string stationfetchdate = Convert.ToDateTime(data.stationreaddate).ToString();
            writer.WriteLine($"Annual Average data from Defra on {stationfetchdate}");
            writer.WriteLine($"Site Name,{data.sitename}");
            writer.WriteLine($"Site Type,{data.siteType}");
            writer.WriteLine($"Region,{data.region}");
            writer.WriteLine($"Latitude,{data.latitude}");
            writer.WriteLine($"Longitude,{data.longitude}");
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

        private void WriteGroupedData(StreamWriter writer, List<pivotpollutant> groupedData, List<string> pollutants)
        {
            foreach (var item in groupedData)
            {
                int year = Convert.ToInt32(item.date);
                DateTime startDate = new DateTime(year, 1, 1);
                DateTime endDate = (year == DateTime.Now.Year) ? DateTime.Now.Date : new DateTime(year, 12, 31);
                string dateRange = $"{startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}";

                writer.Write(dateRange);

                foreach (var pollutant in pollutants)
                {
                    var sub = item.Subpollutant.FirstOrDefault(s => s.pollutantname == pollutant);
                    writer.Write($",{sub?.pollutantvalue ?? ""},{sub?.verification ?? ""}");
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

        private List<pivotpollutant> GroupFinalData(List<Finaldata> finalList)
        {
            return finalList.GroupBy(x => x.ReportDate)
                .Select(y => new pivotpollutant
                {
                    date = y.Key,
                    Subpollutant = y.Select(x => new SubpollutantItem
                    {
                        pollutantname = x.AnnualPollutantname,
                        pollutantvalue = x.Total == 0 ? "no data" : x.Total.ToString(),
                        verification = MapVerification(x.AnnualVerification)
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
