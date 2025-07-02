
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System;
using System.Collections.Generic;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AWSS3BucketServiceTests
    {
        private readonly Mock<ILogger<AWSS3BucketService>> _loggerMock = new();
        private readonly Mock<IHourlyAtomFeedExportCSV> _hourlyMock = new();
        private readonly Mock<IDailyAtomFeedExportCSV> _dailyMock = new();
        private readonly Mock<IAnnualAtomFeedExportCSV> _annualMock = new();
        private readonly Mock<IAWSPreSignedURLService> _preSignedUrlMock = new();

        private readonly AWSS3BucketService _service;

        public AWSS3BucketServiceTests()
        {
            _service = new AWSS3BucketService(
                _loggerMock.Object,
                _hourlyMock.Object,
                _dailyMock.Object,
                _annualMock.Object,
                _preSignedUrlMock.Object
            );
        }

        private List<Finaldata> GetSampleFinalData() => new() { new Finaldata() };
        private querystringdata GetSampleQueryData() => new()
        {
            siteId = "123",
            year = "2023",
            sitename = "TestSite",
            downloadpollutant = "NO2",
            downloadpollutanttype = "Hourly"
        };

        [Fact]
        public void WriteCsvToS3Bucket_Daily_Success()
        {
            var data = GetSampleQueryData();
            var finalList = GetSampleFinalData();
            var csvBytes = new byte[] { 1, 2, 3 };

            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data)).Returns(csvBytes);
            _preSignedUrlMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                             .Returns("https://presigned.url");

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            var result = _service.writecsvtoawss3bucket(finalList, data, "Daily");

            Assert.Equal("https://presigned.url", result);
        }

        [Fact]
        public void WriteCsvToS3Bucket_Annual_Success()
        {
            var data = GetSampleQueryData();
            var finalList = GetSampleFinalData();
            var csvBytes = new byte[] { 4, 5, 6 };

            _annualMock.Setup(x => x.annualatomfeedexport_csv(finalList, data)).Returns(csvBytes);
            _preSignedUrlMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                             .Returns("https://presigned.url");

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            var result = _service.writecsvtoawss3bucket(finalList, data, "Annual");

            Assert.Equal("https://presigned.url", result);
        }

        [Fact]
        public void WriteCsvToS3Bucket_Hourly_Success()
        {
            var data = GetSampleQueryData();
            var finalList = GetSampleFinalData();
            var csvBytes = new byte[] { 7, 8, 9 };

            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(finalList, data)).Returns(csvBytes);
            _preSignedUrlMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                             .Returns("https://presigned.url");

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            var result = _service.writecsvtoawss3bucket(finalList, data, "Hourly");

            Assert.Equal("https://presigned.url", result);
        }

        [Fact]
        public void WriteCsvToS3Bucket_MissingRegion_ThrowsException()
        {
            var data = GetSampleQueryData();
            var finalList = GetSampleFinalData();
            var csvBytes = new byte[] { 1, 2, 3 };

            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(finalList, data)).Returns(csvBytes);
            Environment.SetEnvironmentVariable("AWS_REGION", null);

            var result = _service.writecsvtoawss3bucket(finalList, data, "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void WriteCsvToS3Bucket_CsvExport_ThrowsException()
        {
            var data = GetSampleQueryData();
            var finalList = GetSampleFinalData();

            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(finalList, data)).Throws(new Exception("CSV error"));
            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");

            var result = _service.writecsvtoawss3bucket(finalList, data, "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void WriteCsvToS3Bucket_PreSignedUrl_ThrowsException()
        {
            var data = GetSampleQueryData();
            var finalList = GetSampleFinalData();
            var csvBytes = new byte[] { 1, 2, 3 };

            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(finalList, data)).Returns(csvBytes);
            _preSignedUrlMock.Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                             .Throws(new Exception("URL error"));

            Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            var result = _service.writecsvtoawss3bucket(finalList, data, "Hourly");

            Assert.Equal(string.Empty, result);
        }
    }
}