using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Text;
using System.Collections;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class HourlyAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger) : IHourlyAtomFeedExportCSV
    {
        public async Task<byte[]> hourlyatomfeedexport_csv(List<FinalData> Final_list, QueryStringData data)
        {
            try
            {
                var groupedData = GroupFinalData(Final_list);
                var distinctPollutants = Final_list.Select(s => s.PollutantName).Distinct().OrderBy(m => m).ToList();
                var stationfetchdate = Convert.ToDateTime(data.StationReadDate).ToString();

                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream);

                //using var writer = new StreamWriter("HourlyPivotData.csv");

                WriteCsvHeader(writer, data, stationfetchdate);
                WriteCsvColumnHeaders(writer, distinctPollutants);
                WriteCsvRows(writer, groupedData, distinctPollutants);

                writer.Flush();
                return memoryStream.ToArray();
                // Uncomment for local csv write
                //byte[] byteArray = [];
                //return byteArray;
            }
            catch (Exception ex)
            {
                logger.LogError("Hourly download csv error Info message {Error}", ex.Message);
                logger.LogError("Hourly download csv Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20 };
            }
        }

        private void WriteCsvHeader(StreamWriter writer, QueryStringData data, string stationfetchdate)
        {
            writer.WriteLine($"Hourly data from Defra on {stationfetchdate}");
            writer.WriteLine($"Site Name,{data.SiteName}");
            writer.WriteLine($"Site Type,{data.SiteType}");
            writer.WriteLine($"Region,{data.Region}");
            writer.WriteLine($"Latitude,{data.Latitude}");
            writer.WriteLine($"Longitude,{data.Longitude}");
            writer.WriteLine("Notes:,[1] All Data GMT hour ending;  [2] Some shorthand is used V = Verified P = Provisionally Verified N = Not Verified S = Suspect [3] Unit of measurement (for pollutants) = ugm-3");
        }

        private void WriteCsvColumnHeaders(StreamWriter writer, List<string> pollutants)
        {
            writer.Write("Date,Time");
            foreach (var pollutant in pollutants)
            {
                var header = GetPollutantHeader(pollutant);
                writer.Write($",{header},Status");
            }
            writer.WriteLine();
        }

        private void WriteCsvRows(StreamWriter writer, List<PivotPollutant> groupedData, List<string> pollutants)
        {
            foreach (var item in groupedData)
            {
                writer.Write($"{item.Date},{item.Time}");
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
            return finalList
                .GroupBy(x => new { Date = Convert.ToDateTime(x.StartTime).Date, Time = Convert.ToDateTime(x.StartTime).TimeOfDay })
                .Select(g => new PivotPollutant
                {
                    Date = g.Key.Date.ToString("yyyy-MM-dd"),
                    Time = g.Key.Time.ToString(@"hh\:mm\:ss"),
                    SubPollutant = g.Select(x => new SubPollutantItem
                    {
                        PollutantName = x.PollutantName,
                        PollutantValue = x.Value == "-99" ? "no data" : x.Value,
                        Verification = x.Verification switch
                        {
                            "1" => "V",
                            "2" => "P",
                            "3" => "N",
                            _ => "others"
                        }
                    }).ToList()
                }).ToList();
        }
    }
}
