using Amazon.S3;
using CsvHelper;
using NetTopologySuite.Index.HPRtree;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{

    public class DataSelectionHourlyAtomFeedExportCsv(ILogger<HourlyAtomFeedExportCsv> logger) : IDataSelectionHourlyAtomFeedExportCsv
    {
        public async Task<byte[]> dataSelectionHourlyAtomFeedExportCSV(List<FinalData> Final_list, QueryStringData data)
        {
            try
            {
                //final version prod enviornment use the below code
                logger.LogInformation("zipStream started {Starttime}", DateTime.Now);
                using (var zipStream = new MemoryStream())
                {
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        var zipEntry = archive.CreateEntry("file1.csv", CompressionLevel.Optimal);

                        using (var entryStream = zipEntry.Open())
                        using (var bufferedStream = new BufferedStream(entryStream, 65536))
                        using (var writer = new StreamWriter(bufferedStream))
                        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                        {
                            await csv.WriteRecordsAsync(Final_list.Select(item => new FinalDataCsv
                            {
                                StartTime = item.StartTime?.Trim(),
                                EndTime = item.EndTime?.Trim(),
                                Status = (item.Verification switch
                                {
                                    "1" => "V",
                                    "2" => "P",
                                    "3" => "N",
                                    _ => "others"
                                }),
                                Unit = "ugm-3",
                                Value = (item.Value == "-99" ? "no data" : item.Value)?.Trim(),
                                PollutantName = item.PollutantName?.Trim(),
                                SiteName = item.SiteName?.Trim(),
                                SiteType = item.SiteType?.Trim(),
                                Region = item.Region?.Trim(),
                                Country = item.Country?.Trim()
                            }));
                            await writer.FlushAsync();
                        }
                    }

                    zipStream.Position = 0; // Reset position before reading
                    logger.LogInformation("zipStream ended {Starttime}", DateTime.Now);
                    return zipStream.ToArray(); // Return the zipped content as byte array
                }
            }
            catch (Exception ex)
            {
                logger.LogError("DataSelection Hourly download csv dataSelectionHourlyAtomFeedExportCSV error Info message {Error}", ex.Message);
                logger.LogError("DataSelection Hourly download csv dataSelectionHourlyAtomFeedExportCSV Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20 };
            }
        }
    }
}
