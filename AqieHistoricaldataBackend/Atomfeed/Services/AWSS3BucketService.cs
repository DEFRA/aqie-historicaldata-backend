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

    public class AWSS3BucketService(ILogger<AWSS3BucketService> logger, IHourlyAtomFeedExportCSV hourlyAtomFeedExportCSV,
        IDailyAtomFeedExportCSV dailyAtomFeedExportCSV, IAnnualAtomFeedExportCSV annualAtomFeedExportCSV,
        IAWSPreSignedURLService awsPreSignedURLService, IS3TransferUtility s3TransferUtility) : IAWSS3BucketService
    {
        private readonly IS3TransferUtility s3TransferUtility = s3TransferUtility;

        public async Task<string> writecsvtoawss3bucket(List<FinalData> Final_list, QueryStringData data, string downloadtype)
        {
            try
            {
                var csvBytes = await GetCsvBytesAsync(Final_list, data, downloadtype);
                var region = GetAwsRegion();
                var bucketName = GetBucketName();
                var key = GenerateS3Key(data);

                await UploadToS3Async(csvBytes, bucketName, key);
                return await GeneratePresignedUrlAsync(bucketName, key);
            }
            catch (Exception ex)
            {
                logger.LogError("Error AWS S3 bucket Info message {Error}", ex.Message);
                logger.LogError("Error AWS S3 bucket Info stacktrace {Error}", ex.StackTrace);
                return string.Empty;
            }
        }

        private async Task<byte[]> GetCsvBytesAsync(List<FinalData> finalList, QueryStringData data, string downloadType)
        {
            return downloadType switch
            {
                "Daily" => dailyAtomFeedExportCSV.dailyatomfeedexport_csv(finalList, data),
                "Annual" => await annualAtomFeedExportCSV.annualatomfeedexport_csv(finalList, data),
                _ => await hourlyAtomFeedExportCSV.hourlyatomfeedexport_csv(finalList, data)
            };
        }

        private string GetAwsRegion()
        {
            var region = Environment.GetEnvironmentVariable("AWS_REGION");
            if (string.IsNullOrEmpty(region))
                throw new ArgumentNullException("AWS_REGION");
            return region;
        }

        private string GetBucketName()
        {
            var bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");
            if (string.IsNullOrEmpty(bucketName))
                throw new ArgumentNullException("S3_BUCKET_NAME");
            return bucketName;
        }

        private string GenerateS3Key(QueryStringData data)
        {
            return $"{data.SiteName}_{data.DownloadPollutant}_{data.DownloadPollutantType}_{data.Year}.csv";
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
            var url = await awsPreSignedURLService.GeneratePreSignedURL(bucketName, key, 604800);
            logger.LogInformation("S3 bucket PresignedUrl final URL {PresignedUrl}", url);
            return url;
        }
    }
}
