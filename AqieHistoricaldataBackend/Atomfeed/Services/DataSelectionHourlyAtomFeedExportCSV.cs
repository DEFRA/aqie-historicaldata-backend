using CsvHelper;
using System.Globalization;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class DataSelectionHourlyAtomFeedExportCSV(ILogger<HourlyAtomFeedExportCSV> logger) : IDataSelectionHourlyAtomFeedExportCSV
    {
        public async Task<byte[]> dataSelectionHourlyAtomFeedExportCSV(List<FinalData> Final_list, QueryStringData data)
        {
            try
            {
                //using (var writer = new StreamWriter("file.csv"))
                //using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                //{
                //    csv.WriteRecords(Final_list);
                //}
                //byte[] byteArray = [];
                //return byteArray;
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
