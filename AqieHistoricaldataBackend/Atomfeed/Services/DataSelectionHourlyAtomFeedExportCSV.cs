using CsvHelper;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{

    public class DataSelectionHourlyAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger) : IDataSelectionHourlyAtomFeedExportCSV
    {
        public async Task<byte[]> dataSelectionHourlyAtomFeedExportCSV(List<FinalData> Final_list, QueryStringData data)
        {
            try
            {
                //Previous code for csv generation
                //var finalListCsv = Final_list.Select(item => new FinalDataCsv
                //{
                //    //StartTime = DateTime.TryParse(item.StartTime, out var startDate)? startDate.ToString("yyyy-MM-dd"): null,
                //    //EndTime = DateTime.TryParse(item.EndTime, out var endDate) ? startDate.ToString("yyyy-MM-dd") : null,
                //    StartTime = item.StartTime,
                //    EndTime = item.EndTime,
                //    Status = item.Verification switch
                //    {
                //        "1" => "V",
                //        "2" => "P",
                //        "3" => "N",
                //        _ => "others"
                //    },
                //    Unit = "ugm-3",
                //    Value = item.Value == "-99" ? "no data" : item.Value,
                //    PollutantName = item.PollutantName,
                //    SiteName = item.SiteName,
                //    SiteType = item.SiteType,
                //    Region = item.Region,
                //    Country = item.Country
                //}).ToList();
                //For local csv write
                //using (var writer = new StreamWriter("file1.csv"))
                //using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //{
                //    csv.WriteRecords(finalListCsv);
                //}
                //byte[] byteArray = [];
                //return byteArray;
                //For previous prod code 
                //using (var memoryStream = new MemoryStream())
                //using (var writer = new StreamWriter(memoryStream))
                //using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //{

                //    await csv.WriteRecordsAsync(finalListCsv);
                //    await writer.FlushAsync();
                //    return memoryStream.ToArray();
                //}

                //for local code check zip file creation
                //string zipPath = "file1.zip";
                //string csvFileNameInZip = "file1.csv";

                //using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true))
                //using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create, leaveOpen: false))
                //{
                //    var zipEntry = archive.CreateEntry(csvFileNameInZip, CompressionLevel.Optimal);

                //    using (var entryStream = zipEntry.Open())
                //    using (var bufferedStream = new BufferedStream(entryStream, 65536))
                //    using (var writer = new StreamWriter(bufferedStream))
                //    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //    {
                //        await csv.WriteRecordsAsync(Final_list.Select(item => new FinalDataCsv
                //        {
                //            StartTime = item.StartTime,
                //            EndTime = item.EndTime,
                //            Status = item.Verification switch
                //            {
                //                "1" => "V",
                //                "2" => "P",
                //                "3" => "N",
                //                _ => "others"
                //            },
                //            Unit = "ugm-3",
                //            Value = item.Value == "-99" ? "no data" : item.Value,
                //            PollutantName = item.PollutantName,
                //            SiteName = item.SiteName,
                //            SiteType = item.SiteType,
                //            Region = item.Region,
                //            Country = item.Country
                //        }));
                //    }
                //}
                //byte[] byteArray = [];
                //return byteArray;

                // other option for prod environment use below code 

                //using (var zipStream = new MemoryStream())
                //{
                //    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                //    {
                //        var zipEntry = archive.CreateEntry("file1.csv", CompressionLevel.Optimal);

                //        using (var entryStream = zipEntry.Open())
                //        using (var writer = new StreamWriter(entryStream))
                //        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //        {
                //            await csv.WriteRecordsAsync(finalListCsv);
                //            await writer.FlushAsync(); // Ensure all data is flushed
                //        }
                //    }

                //    zipStream.Position = 0; // Reset stream position before reading
                //    return zipStream.ToArray(); // Return the zipped byte array
                //}

                // another prod environment use below code 

                // Transform the data
                //var finalListCsv = Final_list
                //    .AsParallel() // Optional: parallelize transformation
                //    .Select(item => new FinalDataCsv
                //    {
                //        StartTime = item.StartTime,
                //        EndTime = item.EndTime,
                //        Status = item.Verification switch
                //        {
                //            "1" => "V",
                //            "2" => "P",
                //            "3" => "N",
                //            _ => "others"
                //        },
                //        Unit = "ugm-3",
                //        Value = item.Value == "-99" ? "no data" : item.Value,
                //        PollutantName = item.PollutantName,
                //        SiteName = item.SiteName,
                //        SiteType = item.SiteType,
                //        Region = item.Region,
                //        Country = item.Country
                //    })
                //    .ToList(); // Materialize after parallel processing

                //// Write to CSV and zip it
                //using (var zipStream = new MemoryStream())
                //{
                //    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                //    {
                //        var zipEntry = archive.CreateEntry("file1.csv", CompressionLevel.Optimal);

                //        using (var entryStream = zipEntry.Open())
                //        using (var writer = new StreamWriter(entryStream, bufferSize: 65536)) // Optional buffer size
                //        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //        {
                //            await csv.WriteRecordsAsync(finalListCsv).ConfigureAwait(false);
                //            await writer.FlushAsync().ConfigureAwait(false);
                //        }
                //    }

                //    zipStream.Position = 0;
                //    return zipStream.ToArray();
                //}

                //final prod enviornment use the below code
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
                                StartTime = item.StartTime,
                                EndTime = item.EndTime,
                                Status = item.Verification switch
                                {
                                    "1" => "V",
                                    "2" => "P",
                                    "3" => "N",
                                    _ => "others"
                                },
                                Unit = "ugm-3",
                                Value = item.Value == "-99" ? "no data" : item.Value,
                                PollutantName = item.PollutantName,
                                SiteName = item.SiteName,
                                SiteType = item.SiteType,
                                Region = item.Region,
                                Country = item.Country
                            }));
                            await writer.FlushAsync();
                        }
                    }

                    zipStream.Position = 0; // Reset position before reading
                    return zipStream.ToArray(); // Return the zipped content as byte array
                }

                //final final prod environment code use below code
                // Transform the data (deferred execution, no .ToList())
                //var transformedData = Final_list
                //    .AsParallel()
                //    .Select(item => new FinalDataCsv
                //    {
                //        StartTime = item.StartTime,
                //        EndTime = item.EndTime,
                //        Status = item.Verification switch
                //        {
                //            "1" => "V",
                //            "2" => "P",
                //            "3" => "N",
                //            _ => "others"
                //        },
                //        Unit = "ugm-3",
                //        Value = item.Value == "-99" ? "no data" : item.Value,
                //        PollutantName = item.PollutantName,
                //        SiteName = item.SiteName,
                //        SiteType = item.SiteType,
                //        Region = item.Region,
                //        Country = item.Country
                //    });

                //// Write to CSV and zip it
                //using (var zipStream = new MemoryStream())
                //{
                //    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                //    {
                //        var zipEntry = archive.CreateEntry("file1.csv", CompressionLevel.Optimal);

                //        using (var entryStream = zipEntry.Open())
                //        using (var bufferedStream = new BufferedStream(entryStream, 65536)) // Buffered for performance
                //        using (var writer = new StreamWriter(bufferedStream))
                //        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //        {
                //            await csv.WriteRecordsAsync(transformedData).ConfigureAwait(false);
                //            await writer.FlushAsync().ConfigureAwait(false);
                //        }
                //    }

                //    zipStream.Position = 0;
                //    return zipStream.ToArray();
                //}
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
