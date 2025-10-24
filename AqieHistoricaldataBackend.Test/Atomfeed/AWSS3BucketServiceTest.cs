//using Amazon.S3.Transfer;
//using Microsoft.Extensions.Logging;
//using Moq;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Xunit;
//using AqieHistoricaldataBackend.Atomfeed.Services;
//using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

//namespace AqieHistoricaldataBackend.Test.Atomfeed
//{
//    public class AWSS3BucketServiceTests
//    {
//        private readonly Mock<ILogger<AWSS3BucketService>> _loggerMock = new();
//        private readonly Mock<IHourlyAtomFeedExportCSV> _hourlyMock = new();
//        private readonly Mock<IDailyAtomFeedExportCSV> _dailyMock = new();
//        private readonly Mock<IAnnualAtomFeedExportCSV> _annualMock = new();
//        private readonly Mock<IAWSPreSignedURLService> _presignedUrlMock = new();
//        private readonly Mock<IS3TransferUtility> _s3TransferUtilityMock = new();

//        private AWSS3BucketService CreateService() =>
//            new(_loggerMock.Object, _hourlyMock.Object, _dailyMock.Object, _annualMock.Object, _presignedUrlMock.Object, _s3TransferUtilityMock.Object);

//        public AWSS3BucketServiceTests()
//        {
//            _s3TransferUtilityMock.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
//        }

//        [Fact]
//        public async Task WriteCsvToAwsS3Bucket_Hourly_Success()
//        {
//            var service = CreateService();
//            var finalList = new List<FinalData>();
//            var data = new QueryStringData { SiteName = "Site", DownloadPollutant = "NO2", DownloadPollutantType = "Type", Year = "2023" };
//            var csvBytes = new byte[] { 1, 2, 3 };

//            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(finalList, data)).ReturnsAsync(csvBytes);
//            _presignedUrlMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800)).ReturnsAsync("https://url");

//            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
//            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

//            var result = await service.writecsvtoawss3bucket(finalList, data, "Hourly");

//            Assert.Equal("https://url", result);
//        }

//        [Fact]
//        public async Task WriteCsvToAwsS3Bucket_Daily_Success()
//        {
//            var service = CreateService();
//            var finalList = new List<FinalData>();
//            var data = new QueryStringData { SiteName = "Site", DownloadPollutant = "NO2", DownloadPollutantType = "Type", Year = "2023" };
//            var csvBytes = new byte[] { 1, 2, 3 };

//            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data)).Returns(csvBytes);
//            _presignedUrlMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800)).ReturnsAsync("https://url");

//            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
//            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

//            var result = await service.writecsvtoawss3bucket(finalList, data, "Daily");

//            Assert.Equal("https://url", result);
//        }

//        [Fact]
//        public async Task WriteCsvToAwsS3Bucket_Annual_Success()
//        {
//            var service = CreateService();
//            var finalList = new List<FinalData>();
//            var data = new QueryStringData { SiteName = "Site", DownloadPollutant = "NO2", DownloadPollutantType = "Type", Year = "2023" };
//            var csvBytes = new byte[] { 1, 2, 3 };

//            _annualMock.Setup(x => x.annualatomfeedexport_csv(finalList, data)).ReturnsAsync(csvBytes);
//            _presignedUrlMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800)).ReturnsAsync("https://url");

//            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
//            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

//            var result = await service.writecsvtoawss3bucket(finalList, data, "Annual");

//            Assert.Equal("us-east-1", Environment.GetEnvironmentVariable("AWS_REGION"));
//            Assert.Equal("test-bucket", Environment.GetEnvironmentVariable("S3_BUCKET_NAME"));

//            Assert.Equal("https://url", result);
//        }

//        [Fact]
//        public async Task WriteCsvToAwsS3Bucket_MissingRegion_ReturnsEmpty()
//        {
//            var service = CreateService();
//            var finalList = new List<FinalData>();
//            var data = new QueryStringData();

//            Environment.SetEnvironmentVariable("AWS_REGION", null);
//            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

//            var result = await service.writecsvtoawss3bucket(finalList, data, "Hourly");

//            Assert.Equal(string.Empty, result);
//        }

//        [Fact]
//        public async Task WriteCsvToAwsS3Bucket_MissingBucket_ReturnsEmpty()
//        {
//            var service = CreateService();
//            var finalList = new List<FinalData>();
//            var data = new QueryStringData();

//            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
//            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);

//            var result = await service.writecsvtoawss3bucket(finalList, data, "Hourly");

//            Assert.Equal(string.Empty, result);
//        }

//        [Fact]
//        public async Task WriteCsvToAwsS3Bucket_ExceptionDuringUpload_ReturnsEmpty()
//        {
//            var service = CreateService();
//            var finalList = new List<FinalData>();
//            var data = new QueryStringData();

//            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(finalList, data)).ThrowsAsync(new Exception("Upload failed"));

//            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
//            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

//            var result = await service.writecsvtoawss3bucket(finalList, data, "Hourly");

//            Assert.Equal(string.Empty, result);
//        }
//    }
//}