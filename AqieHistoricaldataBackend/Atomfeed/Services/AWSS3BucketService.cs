using Amazon.S3.Transfer;
using System;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static System.Net.Mime.MediaTypeNames;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AWSS3BucketService(ILogger<AWSS3BucketService> logger
        , IHourlyAtomFeedExportCSV HourlyAtomFeedExportCSV, IDailyAtomFeedExportCSV DailyAtomFeedExportCSV
        , IAnnualAtomFeedExportCSV AnnualAtomFeedExportCSV,
        IAWSPreSignedURLService AWSPreSignedURLService) : IAWSS3BucketService
    {
        //public async Task<string> writecsvtoawss3bucket(dynamic Final_list, querystringdata data)
        public async Task<string> writecsvtoawss3bucket(List<Finaldata> Final_list, querystringdata data, string downloadtype)
        {
            string siteId = data.siteId;
            string year = data.year;
            string PresignedUrl = string.Empty;
            byte[] csvbyte;
            try
            {
                if(downloadtype == "Daily")
                {
                    csvbyte =  DailyAtomFeedExportCSV.dailyatomfeedexport_csv(Final_list, data);
                }
                else if(downloadtype == "Annual")
                {
                    csvbyte = await AnnualAtomFeedExportCSV.annualatomfeedexport_csv(Final_list, data);
                }
                else
                {
                    csvbyte = await HourlyAtomFeedExportCSV.hourlyatomfeedexport_csv(Final_list, data);
                }
                string Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? throw new ArgumentNullException("AWS_REGION");
                logger.LogInformation("S3 bucket name start {s3BucketName}", DateTime.Now);
                string s3BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");
                logger.LogInformation("S3 bucket name end {s3BucketName}", s3BucketName);
                string s3Key = data.sitename + "_" + data.downloadpollutant + "_" + data.downloadpollutanttype + "_" + year + ".csv";
                var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Region);
                logger.LogInformation("S3 bucket region {regionEndpoint}", regionEndpoint);

                using (var s3Client = new Amazon.S3.AmazonS3Client())
                {
                    using (var transferUtility = new TransferUtility(s3Client))
                    {
                        using (var stream = new MemoryStream(csvbyte))
                        {
                            logger.LogInformation("S3 bucket upload start {Starttime}", DateTime.Now);
                            await transferUtility.UploadAsync(stream, s3BucketName, s3Key);
                            logger.LogInformation("S3 bucket upload end {Endtime}", DateTime.Now);
                        }
                    }
                }
                logger.LogInformation("S3 bucket PresignedUrl start {Datetime}", DateTime.Now);
                PresignedUrl = await AWSPreSignedURLService.GeneratePreSignedURL(s3BucketName, s3Key, 604800);
                logger.LogInformation("S3 bucket PresignedUrl final URL {PresignedUrl}", PresignedUrl);
            }
            catch (Exception ex)
            {
                logger.LogError("Error AWS S3 bucket Info message {Error}", ex.Message);
                logger.LogError("Error AWS S3 bucket Info stacktrace {Error}", ex.StackTrace);
            }
            return PresignedUrl;
        }
    }
}
