using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IS3TransferUtility
    {
        Task UploadAsync(Stream stream, string bucketName, string key);
    }

    public class S3TransferUtility : IS3TransferUtility
    {
        public async Task UploadAsync(Stream stream, string bucketName, string key)
        {
            using var s3Client = new Amazon.S3.AmazonS3Client();
            using var transferUtility = new TransferUtility(s3Client);
            await transferUtility.UploadAsync(stream, bucketName, key);
        }
    }

    public class Awss3BucketService(ILogger<Awss3BucketService> logger, IHourlyAtomFeedExportCsv hourlyAtomFeedExportCsv,
        IDailyAtomFeedExportCsv dailyAtomFeedExportCsv, IAnnualAtomFeedExportCsv annualAtomFeedExportCsv,
        IAwsPreSignedUrLService AwsPreSignedUrLService, IS3TransferUtility s3TransferUtility,
        IDataSelectionHourlyAtomFeedExportCsv DataSelectionHourlyAtomFeedExportCsv) : IAwss3BucketService
    {
        private readonly IS3TransferUtility s3TransferUtility = s3TransferUtility;

        public async Task<string> WriteCsvToAwsS3BucketAsync(List<FinalData> finalList, QueryStringData data, string downloadType)
        {
            try
            {
                string bucketName = GetBucketName();
                byte[] csvBytes;
                string key;

                if (data.dataselectorfiltertype == "dataSelectorHourly")
                {
                    logger.LogInformation("dataSelectorHourly entered {Starttime}", DateTime.Now);
                    csvBytes = await GetDataselectorCsvBytesAsync(finalList, data, downloadType);
                    key = DataSelectorGenerateS3Key(data);
                }
                else
                {
                    logger.LogInformation("else dataSelectorHourly entered {Starttime}", DateTime.Now);
                    csvBytes = await GetCsvBytesAsync(finalList, data, downloadType);
                    key = GenerateS3Key(data);
                }

                await UploadToS3Async(csvBytes, bucketName, key);
                return await GeneratePresignedUrlAsync(bucketName, key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error WriteCsvToAwsS3BucketAsync AWS S3 bucket Info message");
                return string.Empty;
            }
        }

        private async Task<byte[]> GetCsvBytesAsync(List<FinalData> finalList, QueryStringData data, string downloadType)
        {
            return downloadType switch
            {
                "Daily" => dailyAtomFeedExportCsv.dailyatomfeedexport_csv(finalList, data),
                "Annual" => await annualAtomFeedExportCsv.annualatomfeedexport_csv(finalList, data),
                _ => await hourlyAtomFeedExportCsv.hourlyatomfeedexport_csv(finalList, data)
            };
        }

        private async Task<byte[]> GetDataselectorCsvBytesAsync(List<FinalData> finalList, QueryStringData data, string downloadType)
        {
            return downloadType switch
            {
                "Daily" => dailyAtomFeedExportCsv.dailyatomfeedexport_csv(finalList, data),
                "Annual" => await annualAtomFeedExportCsv.annualatomfeedexport_csv(finalList, data),
                _ => await DataSelectionHourlyAtomFeedExportCsv.dataSelectionHourlyAtomFeedExportCSV(finalList, data)
            };
        }

        private static string GetBucketName()
        {
            var bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(bucketName))
                throw new InvalidOperationException("Environment variable 'S3_BUCKET_NAME' is not configured.");
            return bucketName;
        }

        private static string GenerateS3Key(QueryStringData data)
        {
            return $"{data.SiteName}_{data.DownloadPollutant}_{data.DownloadPollutantType}_{data.Year}.csv";
        }

        private static string DataSelectorGenerateS3Key(QueryStringData data)
        {
            return $"{data.dataSource}_{data.pollutantName}_{data.Region}_{data.Year}.zip";
        }

        private async Task UploadToS3Async(byte[] csvBytes, string bucketName, string key)
        {
            logger.LogInformation("S3 bucket upload start {Starttime}", DateTime.Now);
            using var stream = new MemoryStream(csvBytes);
            await s3TransferUtility.UploadAsync(stream, bucketName, key);
            logger.LogInformation("S3 bucket upload end {Endtime}", DateTime.Now);
        }

        private async Task<string> GeneratePresignedUrlAsync(string bucketName, string key)
        {
            logger.LogInformation("S3 bucket PresignedUrl start {Datetime}", DateTime.Now);
            // Expire in 2 days (172800 seconds) for 7days (604800 seconds)
            var url = await AwsPreSignedUrLService.GeneratePreSignedURL(bucketName, key, 604800);
            logger.LogInformation("S3 bucket PresignedUrl final URL {PresignedUrl}", url);
            return url;
        }

    }
}
