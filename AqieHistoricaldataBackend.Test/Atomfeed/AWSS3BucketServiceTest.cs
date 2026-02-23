using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AWSS3BucketServiceTests
    {
        private readonly Mock<ILogger<AWSS3BucketService>> _loggerMock = new();
        private readonly Mock<IHourlyAtomFeedExportCSV> _hourlyMock = new();
        private readonly Mock<IDailyAtomFeedExportCSV> _dailyMock = new();
        private readonly Mock<IAnnualAtomFeedExportCSV> _annualMock = new();
        private readonly Mock<IAWSPreSignedURLService> _presignedUrlMock = new();
        private readonly Mock<IS3TransferUtility> _s3TransferUtilityMock = new();
        private readonly Mock<IDataSelectionHourlyAtomFeedExportCSV> _dataSelectionHourlyMock = new();

        private static readonly byte[] SampleCsvBytes = new byte[] { 1, 2, 3 };
        private const string PresignedUrl = "https://presigned.url/file";
        private const string BucketName = "test-bucket";
        private const string AwsRegion = "eu-west-1";

        private AWSS3BucketService CreateService() =>
            new(_loggerMock.Object, _hourlyMock.Object, _dailyMock.Object, _annualMock.Object,
                _presignedUrlMock.Object, _s3TransferUtilityMock.Object, _dataSelectionHourlyMock.Object);

        public AWSS3BucketServiceTests()
        {
            _s3TransferUtilityMock
                .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _presignedUrlMock
                .Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                .ReturnsAsync(PresignedUrl);

            SetEnvVars(AwsRegion, BucketName);
        }

        private static void SetEnvVars(string region, string bucket)
        {
            Environment.SetEnvironmentVariable("AWS_REGION", region);
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", bucket);
        }

        // ─── Non-dataSelectorHourly branch ───────────────────────────────────────

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_StandardBranch_HourlyDownloadType_ReturnsPresignedUrl()
        {
            var data = new QueryStringData
            {
                SiteName = "Site1", DownloadPollutant = "NO2",
                DownloadPollutantType = "Type1", Year = "2023"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ReturnsAsync(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_StandardBranch_DefaultDownloadType_CallsHourlyExport()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "O3",
                DownloadPollutantType = "T", Year = "2022"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ReturnsAsync(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Unknown");

            _hourlyMock.Verify(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data), Times.Once);
            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_StandardBranch_DailyDownloadType_CallsDailyExport()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                SiteName = "Site", DownloadPollutant = "PM10",
                DownloadPollutantType = "Hourly", Year = "2023"
            };
            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data))
                      .Returns(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(finalList, data, "Daily");

            _dailyMock.Verify(x => x.dailyatomfeedexport_csv(finalList, data), Times.Once);
            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_StandardBranch_AnnualDownloadType_CallsAnnualExport()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                SiteName = "Site", DownloadPollutant = "PM2.5",
                DownloadPollutantType = "Annual", Year = "2021"
            };
            _annualMock.Setup(x => x.annualatomfeedexport_csv(finalList, data))
                       .ReturnsAsync(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(finalList, data, "Annual");

            _annualMock.Verify(x => x.annualatomfeedexport_csv(finalList, data), Times.Once);
            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_StandardBranch_GeneratesCorrectS3Key()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                SiteName = "MySite", DownloadPollutant = "SO2",
                DownloadPollutantType = "MyType", Year = "2020"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ReturnsAsync(SampleCsvBytes);

            await CreateService().writecsvtoawss3bucket(finalList, data, "Hourly");

            _s3TransferUtilityMock.Verify(
                x => x.UploadAsync(It.IsAny<Stream>(), BucketName, "MySite_SO2_MyType_2020.csv"),
                Times.Once);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_StandardBranch_CallsUploadWithCorrectBucket()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "NO2",
                DownloadPollutantType = "T", Year = "2023"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ReturnsAsync(SampleCsvBytes);

            await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            _s3TransferUtilityMock.Verify(
                x => x.UploadAsync(It.IsAny<Stream>(), BucketName, It.IsAny<string>()),
                Times.Once);
        }

        // ─── dataSelectorHourly branch ────────────────────────────────────────────

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DataSelectorHourly_SingleDownload_HourlyType_CallsDataSelectionExport()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                dataselectorfiltertype = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorSingle",
                dataSource = "DS1", pollutantName = "NO2",
                Region = "London", Year = "2023"
            };
            _dataSelectionHourlyMock
                .Setup(x => x.dataSelectionHourlyAtomFeedExportCSV(finalList, data))
                .ReturnsAsync(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(finalList, data, "Hourly");

            _dataSelectionHourlyMock.Verify(
                x => x.dataSelectionHourlyAtomFeedExportCSV(finalList, data), Times.Once);
            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DataSelectorHourly_SingleDownload_DailyType_CallsDailyExport()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                dataselectorfiltertype = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorSingle",
                dataSource = "DS1", pollutantName = "PM10",
                Region = "North", Year = "2022"
            };
            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(finalList, data))
                      .Returns(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(finalList, data, "Daily");

            _dailyMock.Verify(x => x.dailyatomfeedexport_csv(finalList, data), Times.Once);
            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DataSelectorHourly_SingleDownload_AnnualType_CallsAnnualExport()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                dataselectorfiltertype = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorSingle",
                dataSource = "DS2", pollutantName = "O3",
                Region = "South", Year = "2021"
            };
            _annualMock.Setup(x => x.annualatomfeedexport_csv(finalList, data))
                       .ReturnsAsync(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(finalList, data, "Annual");

            _annualMock.Verify(x => x.annualatomfeedexport_csv(finalList, data), Times.Once);
            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DataSelectorHourly_ElseBranch_CallsDataSelectionExport()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                dataselectorfiltertype = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorMultiple",
                dataSource = "DS3", pollutantName = "SO2",
                Region = "East", Year = "2023"
            };
            _dataSelectionHourlyMock
                .Setup(x => x.dataSelectionHourlyAtomFeedExportCSV(finalList, data))
                .ReturnsAsync(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(finalList, data, "Hourly");

            _dataSelectionHourlyMock.Verify(
                x => x.dataSelectionHourlyAtomFeedExportCSV(finalList, data), Times.Once);
            Assert.Equal(PresignedUrl, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DataSelectorHourly_GeneratesZipS3Key()
        {
            var finalList = new List<FinalData>();
            var data = new QueryStringData
            {
                dataselectorfiltertype = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorSingle",
                dataSource = "MySource", pollutantName = "NO2",
                Region = "London", Year = "2023"
            };
            _dataSelectionHourlyMock
                .Setup(x => x.dataSelectionHourlyAtomFeedExportCSV(finalList, data))
                .ReturnsAsync(SampleCsvBytes);

            await CreateService().writecsvtoawss3bucket(finalList, data, "Hourly");

            _s3TransferUtilityMock.Verify(
                x => x.UploadAsync(It.IsAny<Stream>(), BucketName, "MySource_NO2_London_2023.zip"),
                Times.Once);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DataSelectorHourly_PresignedUrlIsReturned()
        {
            var data = new QueryStringData
            {
                dataselectorfiltertype = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorSingle",
                dataSource = "DS", pollutantName = "PM2.5",
                Region = "West", Year = "2022"
            };
            _dataSelectionHourlyMock
                .Setup(x => x.dataSelectionHourlyAtomFeedExportCSV(It.IsAny<List<FinalData>>(), data))
                .ReturnsAsync(SampleCsvBytes);

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            Assert.Equal(PresignedUrl, result);
            _presignedUrlMock.Verify(
                x => x.GeneratePreSignedURL(BucketName, It.IsAny<string>(), 604800),
                Times.Once);
        }

        // ─── Environment variable guard tests ────────────────────────────────────

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_MissingAwsRegion_ReturnsEmpty()
        {
            SetEnvVars(null, BucketName);

            var result = await CreateService()
                .writecsvtoawss3bucket(new List<FinalData>(), new QueryStringData(), "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_EmptyAwsRegion_ReturnsEmpty()
        {
            SetEnvVars(string.Empty, BucketName);

            var result = await CreateService()
                .writecsvtoawss3bucket(new List<FinalData>(), new QueryStringData(), "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_MissingBucketName_ReturnsEmpty()
        {
            SetEnvVars(AwsRegion, null);

            var result = await CreateService()
                .writecsvtoawss3bucket(new List<FinalData>(), new QueryStringData(), "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_EmptyBucketName_ReturnsEmpty()
        {
            SetEnvVars(AwsRegion, string.Empty);

            var result = await CreateService()
                .writecsvtoawss3bucket(new List<FinalData>(), new QueryStringData(), "Hourly");

            Assert.Equal(string.Empty, result);
        }

        // ─── Exception-path tests ─────────────────────────────────────────────────

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_HourlyExportThrows_ReturnsEmpty()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "NO2",
                DownloadPollutantType = "T", Year = "2023"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ThrowsAsync(new Exception("hourly export failed"));

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DailyExportThrows_ReturnsEmpty()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "PM10",
                DownloadPollutantType = "T", Year = "2023"
            };
            _dailyMock.Setup(x => x.dailyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                      .Throws(new Exception("daily export failed"));

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Daily");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_AnnualExportThrows_ReturnsEmpty()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "PM2.5",
                DownloadPollutantType = "T", Year = "2023"
            };
            _annualMock.Setup(x => x.annualatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ThrowsAsync(new Exception("annual export failed"));

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Annual");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_UploadThrows_ReturnsEmpty()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "NO2",
                DownloadPollutantType = "T", Year = "2023"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ReturnsAsync(SampleCsvBytes);
            _s3TransferUtilityMock
                .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("S3 upload failed"));

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_PresignedUrlThrows_ReturnsEmpty()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "NO2",
                DownloadPollutantType = "T", Year = "2023"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ReturnsAsync(SampleCsvBytes);
            _presignedUrlMock
                .Setup(x => x.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), 604800))
                .ThrowsAsync(new Exception("presigned URL failed"));

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_DataSelectorExportThrows_ReturnsEmpty()
        {
            var data = new QueryStringData
            {
                dataselectorfiltertype = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorSingle",
                dataSource = "DS", pollutantName = "NO2",
                Region = "London", Year = "2023"
            };
            _dataSelectionHourlyMock
                .Setup(x => x.dataSelectionHourlyAtomFeedExportCSV(It.IsAny<List<FinalData>>(), data))
                .ThrowsAsync(new Exception("dataselector export failed"));

            var result = await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            Assert.Equal(string.Empty, result);
        }

        // ─── Error logging verification ───────────────────────────────────────────

        [Fact]
        public async Task WriteCsvToAwsS3Bucket_OnException_LogsErrorMessageAndStackTrace()
        {
            var data = new QueryStringData
            {
                SiteName = "S", DownloadPollutant = "NO2",
                DownloadPollutantType = "T", Year = "2023"
            };
            _hourlyMock.Setup(x => x.hourlyatomfeedexport_csv(It.IsAny<List<FinalData>>(), data))
                       .ThrowsAsync(new Exception("test error"));

            await CreateService().writecsvtoawss3bucket(new List<FinalData>(), data, "Hourly");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("writecsvtoawss3bucket")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ─── S3TransferUtility unit tests ─────────────────────────────────────────

        [Fact]
        public async Task S3TransferUtility_UploadAsync_InvokesCorrectMethod()
        {
            // The concrete S3TransferUtility creates a real AmazonS3Client which
            // requires credentials; we test the interface contract via the mock instead.
            var mockUtility = new Mock<IS3TransferUtility>();
            mockUtility
                .Setup(x => x.UploadAsync(It.IsAny<Stream>(), "bucket", "key"))
                .Returns(Task.CompletedTask);

            using var stream = new MemoryStream(new byte[] { 0x01 });
            await mockUtility.Object.UploadAsync(stream, "bucket", "key");

            mockUtility.Verify(
                x => x.UploadAsync(It.IsAny<Stream>(), "bucket", "key"),
                Times.Once);
        }
    }
}