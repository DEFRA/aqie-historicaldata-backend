using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AWSS3BucketServiceTests
    {
        private readonly Mock<ILogger<AWSS3BucketService>> _loggerMock = new();
        private readonly Mock<IHourlyAtomFeedExportCSV> _hourlyMock = new();
        private readonly Mock<IDailyAtomFeedExportCSV> _dailyMock = new();
        private readonly Mock<IAnnualAtomFeedExportCSV> _annualMock = new();
        private readonly Mock<IAWSPreSignedURLService> _presignedMock = new();

        private AWSS3BucketService CreateService() =>
            new(_loggerMock.Object, _hourlyMock.Object, _dailyMock.Object, _annualMock.Object, _presignedMock.Object);

        private querystringdata GetTestQueryData() => new()
        {
            siteId = "123",
            year = "2025",
            sitename = "TestSite",
            downloadpollutant = "NO2",
            downloadpollutanttype = "avg"
        };

        [Fact]
        public async Task WriteCsvToS3Bucket_DailyDownloadType_CallsDailyExport()
        {
            var service = CreateService();
            var data = GetTestQueryData();
            var finalList = new List<Finaldata>();
            var expectedBytes = new byte[] { 1, 2, 3 };

            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data)).Returns(expectedBytes);
            _presignedMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                          .ReturnsAsync("https://mock-url");

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            var result = await service.writecsvtoawss3bucket(finalList, data, "Daily");

            Assert.Equal("https://mock-url", result);
            _dailyMock.Verify(x => x.dailyatomfeedexport_csv(finalList, data), Times.Once);
        }

        [Fact]
        public async Task WriteCsvToS3Bucket_AnnualDownloadType_CallsAnnualExport()
        {
            var service = CreateService();
            var data = GetTestQueryData();
            var finalList = new List<Finaldata>();
            var expectedBytes = new byte[] { 4, 5, 6 };

            _annualMock.Setup(x => x.annualatomfeedexport_csv(finalList, data)).Returns(expectedBytes);
            _presignedMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                          .ReturnsAsync("https://annual-url");

            Environment.SetEnvironmentVariable("AWS_REGION", "us-west-2");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "annual-bucket");

            var result = await service.writecsvtoawss3bucket(finalList, data, "Annual");

            Assert.Equal("https://annual-url", result);
            _annualMock.Verify(x => x.annualatomfeedexport_csv(finalList, data), Times.Once);
        }

        [Fact]
        public async Task WriteCsvToS3Bucket_HourlyDownloadType_CallsHourlyExport()
        {
            var service = CreateService();
            var data = GetTestQueryData();
            var finalList = new List<Finaldata>();
            var expectedBytes = new byte[] { 7, 8, 9 };

            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(finalList, data)).Returns(expectedBytes);
            _presignedMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                          .ReturnsAsync("https://hourly-url");

            Environment.SetEnvironmentVariable("AWS_REGION", "eu-central-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "hourly-bucket");

            var result = await service.writecsvtoawss3bucket(finalList, data, "Hourly");

            Assert.Equal("https://hourly-url", result);
            _hourlyMock.Verify(x => x.hourlyatomfeedexport_csv(finalList, data), Times.Once);
        }

        [Fact]
        public async Task WriteCsvToS3Bucket_MissingAWSRegion_ThrowsException()
        {
            var service = CreateService();
            var data = GetTestQueryData();
            var finalList = new List<Finaldata>();

            Environment.SetEnvironmentVariable("AWS_REGION", null);

            var result = await service.writecsvtoawss3bucket(finalList, data, "Daily");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToS3Bucket_MissingS3BucketName_StillHandlesGracefully()
        {
            var service = CreateService();
            var data = GetTestQueryData();
            var finalList = new List<Finaldata>();
            var expectedBytes = new byte[] { 1, 2, 3 };

            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data)).Returns(expectedBytes);
            _presignedMock.Setup(x => x.GeneratePreSignedURL(null, It.IsAny<string>(), 604800))
                          .ReturnsAsync("https://null-bucket-url");

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);

            var result = await service.writecsvtoawss3bucket(finalList, data, "Daily");

            Assert.Equal("https://null-bucket-url", result);
        }

        [Fact]
        public async Task WriteCsvToS3Bucket_ExceptionDuringPresignedUrl_ReturnsEmpty()
        {
            var service = CreateService();
            var data = GetTestQueryData();
            var finalList = new List<Finaldata>();
            var expectedBytes = new byte[] { 1, 2, 3 };

            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data)).Returns(expectedBytes);
            _presignedMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                          .ThrowsAsync(new Exception("Presigned error"));

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            var result = await service.writecsvtoawss3bucket(finalList, data, "Daily");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToS3Bucket_EmptyFinalList_StillProcesses()
        {
            var service = CreateService();
            var data = GetTestQueryData();
            var finalList = new List<Finaldata>();
            var expectedBytes = new byte[] { };

            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data)).Returns(expectedBytes);
            _presignedMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                          .ReturnsAsync("https://empty-url");

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            var result = await service.writecsvtoawss3bucket(finalList, data, "Daily");

            Assert.Equal("https://empty-url", result);
        }
    }
}