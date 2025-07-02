using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Text;
using System.Collections;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class HourlyAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger) : IHourlyAtomFeedExportCSV
    {
        public async Task<byte[]> hourlyatomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        {
            try
            {
                var groupedData = GroupFinalData(Final_list);
                var distinctPollutants = Final_list.Select(s => s.Pollutantname).Distinct().OrderBy(m => m).ToList();
                var stationfetchdate = Convert.ToDateTime(data.stationreaddate).ToString();

                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream);

                //using var writer = new StreamWriter("HourlyPivotData.csv");

                WriteCsvHeader(writer, data, stationfetchdate);
                WriteCsvColumnHeaders(writer, distinctPollutants);
                WriteCsvRows(writer, groupedData, distinctPollutants);

                writer.Flush();
                return memoryStream.ToArray();
                // Uncomment for local csv write
                // byte[] byteArray = [];
                // return byteArray;
            }
            catch (Exception ex)
            {
                logger.LogError("Hourly download csv error Info message {Error}", ex.Message);
                logger.LogError("Hourly download csv Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20 };
            }
        }

        private void WriteCsvHeader(StreamWriter writer, querystringdata data, string stationfetchdate)
        {
            writer.WriteLine($"Hourly data from Defra on {stationfetchdate}");
            writer.WriteLine($"Site Name,{data.sitename}");
            writer.WriteLine($"Site Type,{data.siteType}");
            writer.WriteLine($"Region,{data.region}");
            writer.WriteLine($"Latitude,{data.latitude}");
            writer.WriteLine($"Longitude,{data.longitude}");
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

        private void WriteCsvRows(StreamWriter writer, List<pivotpollutant> groupedData, List<string> pollutants)
        {
            foreach (var item in groupedData)
            {
                writer.Write($"{item.date},{item.time}");
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
            return finalList
                .GroupBy(x => new { Date = Convert.ToDateTime(x.StartTime).Date, Time = Convert.ToDateTime(x.StartTime).TimeOfDay })
                .Select(g => new pivotpollutant
                {
                    date = g.Key.Date.ToString("yyyy-MM-dd"),
                    time = g.Key.Time.ToString(@"hh\:mm\:ss"),
                    Subpollutant = g.Select(x => new SubpollutantItem
                    {
                        pollutantname = x.Pollutantname,
                        pollutantvalue = x.Value == "-99" ? "no data" : x.Value,
                        verification = x.Verification switch
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
