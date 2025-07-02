using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class DailyAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger) : IDailyAtomFeedExportCSV
    {
        public byte[] dailyatomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        {
            try
            {
                var groupedData = GroupDataByDate(Final_list);
                var distinctPollutants = Final_list
                    .Select(s => s.DailyPollutantname)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();

                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream);

                //using var writer = new StreamWriter("DailyPivotData.csv");

                WriteMetadata(writer, data);
                WriteHeaders(writer, distinctPollutants);
                WriteData(writer, groupedData, distinctPollutants);

                writer.Flush();
                return memoryStream.ToArray();
                // Uncomment for local csv write
                // byte[] byteArray = [];
                // return byteArray;
            }
            catch (Exception ex)
            {
                logger.LogError("Daily download csv Info message {Error}", ex.Message);
                logger.LogError("Daily download csv Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20 };
            }
        }

        private List<pivotpollutant> GroupDataByDate(List<Finaldata> finalList)
        {
            return finalList
                .GroupBy(x => Convert.ToDateTime(x.ReportDate).Date)
                .Select(y => new pivotpollutant
                {
                    date = y.Key.ToString("yyyy-MM-dd"),
                    Subpollutant = y.Select(x => new SubpollutantItem
                    {
                        pollutantname = x.DailyPollutantname,
                        pollutantvalue = x.Total == 0 ? "no data" : x.Total.ToString(),
                        verification = x.DailyVerification switch
                        {
                            "1" => "V",
                            "2" => "P",
                            "3" => "N",
                            _ => "others"
                        }
                    }).ToList()
                }).ToList();
        }

        private void WriteMetadata(StreamWriter writer, querystringdata data)
        {
            string stationDate = Convert.ToDateTime(data.stationreaddate).ToString();
            writer.WriteLine($"Daily data from Defra on {stationDate}");
            writer.WriteLine($"Site Name,{data.sitename}");
            writer.WriteLine($"Site Type,{data.siteType}");
            writer.WriteLine($"Region,{data.region}");
            writer.WriteLine($"Latitude,{data.latitude}");
            writer.WriteLine($"Longitude,{data.longitude}");
            writer.WriteLine("Notes:,[1] All Data GMT hour ending;  [2] Some shorthand is used V = Verified P = Provisionally Verified N = Not Verified S = Suspect [3] Unit of measurement (for pollutants) = ugm-3");
        }

        private void WriteHeaders(StreamWriter writer, List<string> distinctPollutants)
        {
            writer.Write("Date");
            foreach (var pollutant in distinctPollutants)
            {
                string header = pollutant switch
                {
                    "PM10" => "PM10 particulate matter (Hourly measured)",
                    "PM2.5" => "PM2.5 particulate matter (Hourly measured)",
                    _ => pollutant
                };
                writer.Write($",{header},Status");
            }
            writer.WriteLine();
        }

        private void WriteData(StreamWriter writer, List<pivotpollutant> groupedData, List<string> distinctPollutants)
        {
            foreach (var item in groupedData)
            {
                writer.Write(item.date);
                foreach (var pollutant in distinctPollutants)
                {
                    var sub = item.Subpollutant.FirstOrDefault(s => s.pollutantname == pollutant);
                    writer.Write($",{sub?.pollutantvalue ?? ""},{sub?.verification ?? ""}");
                }
                writer.WriteLine();
            }
        }
    }
}
