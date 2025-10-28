using CsvHelper;
using System.Globalization;
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
                var finalListCsv = Final_list.Select(item => new FinalDataCsv
                {
                    //StartTime = DateTime.TryParse(item.StartTime, out var startDate)? startDate.ToString("yyyy-MM-dd"): null,
                    //EndTime = DateTime.TryParse(item.EndTime, out var endDate) ? startDate.ToString("yyyy-MM-dd") : null,
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
                    Value = item.Value,
                    PollutantName = item.PollutantName,
                    SiteName = item.SiteName,
                    SiteType = item.SiteType,
                    Region = item.Region,
                    Country = item.Country
                }).ToList();
                //For local csv write
                //using (var writer = new StreamWriter("file1.csv"))
                //using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //{
                //    csv.WriteRecords(finalListCsv);
                //}
                //byte[] byteArray = [];
                //return byteArray;
                //For build to CDP
                using (var memoryStream = new MemoryStream())
                using (var writer = new StreamWriter(memoryStream))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {

                    await csv.WriteRecordsAsync(Final_list);
                    await writer.FlushAsync();
                    return memoryStream.ToArray();
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
