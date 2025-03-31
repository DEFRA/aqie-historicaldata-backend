using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class DailyAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger, IHttpClientFactory httpClientFactory) : IDailyAtomFeedExportCSV
    {
        public async Task<byte[]> dailyatomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        {
            try
            {
                string pollutantnameheaderchange = string.Empty;
                string stationfetchdate = data.stationreaddate;
                string region = data.region;
                string siteType = data.siteType;
                string sitename = data.sitename;
                string latitude = data.latitude;
                string longitude = data.longitude;
                var groupedData = Final_list.GroupBy(x => new { Convert.ToDateTime(x.ReportDate).Date})
                            .Select(y => new pivotpollutant
                            {
                                date = y.Key.Date.ToString("yyyy-MM-dd"),
                                Subpollutant = y.Select(x => new SubpollutantItem
                                {
                                    pollutantname = x.DailyPollutantname,
                                    pollutantvalue = x.Total == -99 ? "no data" : x.Total.ToString(),
                                    verification = x.DailyVerification == "1" ? "V" :
                                                   x.DailyVerification == "2" ? "P" :
                                                   x.DailyVerification == "3" ? "N" : "others"
                                }).ToList()
                            }).ToList();

                var distinctpollutant = Final_list.Select(s => s.DailyPollutantname).Distinct().OrderBy(m => m).ToList();
                // Write to MemoryStream
                using (var memoryStream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(memoryStream))
                    {
                //        using (var writer = new StreamWriter("DailyPivotData.csv"))
                //{
                    writer.WriteLine(string.Format("Daily data from Defra on " + stationfetchdate + ""));
                    writer.WriteLine(string.Format("Site Name,{0}", sitename));
                    writer.WriteLine(string.Format("Site Type,{0}", siteType));
                    writer.WriteLine(string.Format("Region,{0}", region));
                    writer.WriteLine(string.Format("Latitude,{0}", latitude));
                    writer.WriteLine(string.Format("Longitude,{0}", longitude));
                    writer.WriteLine(string.Format("Notes:,{0}", "[1] All Data GMT hour ending;  [2] Some shorthand is used V = Verified P = Provisionally Verified N = Not Verified S = Suspect [3] Unit of measurement (for pollutants) = ugm-3"));
                    // Write headers
                    writer.Write("Date");
                    foreach (var pollutantname in distinctpollutant)
                    {
                        if (pollutantname == "PM10")
                        {
                            pollutantnameheaderchange = "PM10 particulate matter (Hourly measured)";
                            writer.Write($",{pollutantnameheaderchange},{"Status"}");
                        }
                        else if (pollutantname == "PM2.5")
                        {
                            pollutantnameheaderchange = "PM2.5 particulate matter (Hourly measured)";
                            writer.Write($",{pollutantnameheaderchange},{"Status"}");
                        }
                        else
                        {
                            writer.Write($",{pollutantname},{"Status"}");
                        }
                    }
                    writer.WriteLine();
                    // Write data
                    foreach (var item in groupedData)
                    {
                        writer.Write($"{item.date}");

                        foreach (var pollutant in distinctpollutant)
                        {
                            var subpollutantvalue = item.Subpollutant.FirstOrDefault(s => s.pollutantname == pollutant);
                            writer.Write($",{subpollutantvalue?.pollutantvalue ?? ""},{subpollutantvalue?.verification ?? ""}");
                        }
                        writer.WriteLine();
                    }

                    writer.Flush(); // Ensure all data is written to the MemoryStream

                    // Convert MemoryStream to byte array
                    //byte[] byteArray = memoryStream.ToArray();
                    byte[] byteArray = [];

                    // Output the byte array (for demonstration purposes)
                    //Console.WriteLine(BitConverter.ToString(byteArray));
                    return byteArray;
                }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error csv Info message {Error}", ex.Message);
                logger.LogError("Error csv Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20 };
            }
        }
    }
}
