using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AnnualAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger) : IAnnualAtomFeedExportCSV
    {
        public byte[] annualatomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        {
            try
            {
                string pollutantnameheaderchange = string.Empty;
                string stationfetchdate = Convert.ToDateTime(data.stationreaddate).ToString(); //2025-03-20T06:05:20.893Z
                string region = data.region;
                string siteType = data.siteType;
                string sitename = data.sitename;
                string latitude = data.latitude;
                string longitude = data.longitude;
                var groupedData = Final_list.GroupBy(x => new { x.ReportDate })
                            .Select(y => new pivotpollutant
                            {
                                date = y.Key.ReportDate,
                                Subpollutant = y.Select(x => new SubpollutantItem
                                {
                                    pollutantname = x.AnnualPollutantname,
                                    pollutantvalue = x.Total == 0 ? "no data" : x.Total.ToString(),
                                    verification = x.AnnualVerification == "1" ? "V" :
                                                   x.AnnualVerification == "2" ? "P" :
                                                   x.AnnualVerification == "3" ? "N" : "others"
                                }).ToList()
                            }).ToList();

                var distinctpollutant = Final_list.Select(s => s.AnnualPollutantname).Distinct().OrderBy(m => m).ToList();
                // Write to MemoryStream
                //using (var memoryStream = new MemoryStream())
                //{
                //    using (var writer = new StreamWriter(memoryStream))
                //    {
                //To check the csv writing to the local folder
                using (var writer = new StreamWriter("AnnualPivotData.csv"))
                {
                    writer.WriteLine(string.Format("Annual Average data from Defra on " + stationfetchdate + ""));
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
                            int year = Convert.ToInt32(item.date);
                            DateTime startDate = new DateTime(year, 1, 1);
                            DateTime endDate;

                            // Check if the year is the current year
                            if (year == DateTime.Now.Year)
                            {
                                endDate = DateTime.Now.Date;
                            }
                            else
                            {
                                endDate = new DateTime(year, 12, 31);
                            }

                            string dateresult = $"{startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}";
                            writer.Write($"{dateresult}");

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
                //}
            }
            catch (Exception ex)
            {
                logger.LogError("Annaul download csv error Info message {Error}", ex.Message);
                logger.LogError("Annaul download csv error Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20 };
            }
        }
    }
}
