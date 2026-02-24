//using System;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
//using Moq;
//using Xunit;
//using AqieHistoricaldataBackend.Atomfeed.Services;
//using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

//namespace AqieHistoricaldataBackend.Test.Atomfeed
//{
//    public class AtomDataSelectionServiceTests
//    {
//        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
//        private readonly Mock<IAtomHourlyFetchService> _atomHourlyFetchServiceMock;
//        private readonly Mock<IAtomDataSelectionStationService> _atomDataSelectionStationServiceMock;
//        private readonly AtomDataSelectionService _service;

//        public AtomDataSelectionServiceTests()
//        {
//            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
//            _atomHourlyFetchServiceMock = new Mock<IAtomHourlyFetchService>();
//            _atomDataSelectionStationServiceMock = new Mock<IAtomDataSelectionStationService>();

//            _service = new AtomDataSelectionService(
//                _loggerMock.Object,
//                _atomHourlyFetchServiceMock.Object,
//                _atomDataSelectionStationServiceMock.Object
//            );
//        }

//        // ----------------------------------------------------------------
//        // Happy-path: all fields populated, station service returns count
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_ReturnsStationResult_WhenAllFieldsProvided()
//        {
//            var data = new QueryStringData
//            {
//                pollutantName = "NO2",
//                dataSource = "AURN",
//                Year = "2024",
//                Region = "London",
//                regiontype = "governmentRegion",
//                dataselectorfiltertype = "dataSelectorCount",
//                dataselectordownloadtype = "dataSelectorSingle",
//                email = "test@example.com"
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    "NO2", "AURN", "2024", "London",
//                    "governmentRegion", "dataSelectorCount",
//                    "dataSelectorSingle", "test@example.com"))
//                .ReturnsAsync("42");

//            var result = await _service.GetatomDataSelectiondata(data);

//            Assert.Equal("42", result);
//        }

//        // ----------------------------------------------------------------
//        // Station service returns "Failure" string — passes through as-is
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_ReturnsFailureString_WhenStationServiceReturnsFailure()
//        {
//            var data = new QueryStringData
//            {
//                pollutantName = "PM10",
//                dataSource = "AURN",
//                Year = "2023",
//                Region = "North West",
//                regiontype = "governmentRegion",
//                dataselectorfiltertype = "dataSelectorHourly",
//                dataselectordownloadtype = "dataSelectorMultiple",
//                email = "user@test.com"
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync("Failure");

//            var result = await _service.GetatomDataSelectiondata(data);

//            Assert.Equal("Failure", result);
//        }

//        // ----------------------------------------------------------------
//        // Station service returns a job GUID for async download scenario
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_ReturnsJobId_WhenStationServiceReturnsGuid()
//        {
//            var jobId = Guid.NewGuid().ToString("N");
//            var data = new QueryStringData
//            {
//                pollutantName = "SO2",
//                dataSource = "AURN",
//                Year = "2022",
//                Region = "South East",
//                regiontype = "governmentRegion",
//                dataselectorfiltertype = "dataSelectorHourly",
//                dataselectordownloadtype = "dataSelectorSingle",
//                email = "job@test.com"
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(jobId);

//            var result = await _service.GetatomDataSelectiondata(data);

//            Assert.Equal(jobId, result);
//        }

//        // ----------------------------------------------------------------
//        // All QueryStringData fields are null — station service is still
//        // called with null values and the returned value is passed through
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_ReturnsResult_WhenAllQueryFieldsAreNull()
//        {
//            var data = new QueryStringData
//            {
//                pollutantName = null,
//                dataSource = null,
//                Year = null,
//                Region = null,
//                regiontype = null,
//                dataselectorfiltertype = null,
//                dataselectordownloadtype = null,
//                email = null
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    null, null, null, null, null, null, null, null))
//                .ReturnsAsync("0");

//            var result = await _service.GetatomDataSelectiondata(data);

//            Assert.Equal("0", result);
//        }

//        // ----------------------------------------------------------------
//        // Station service throws — catch block returns "Failure" and logs
//        // both ex.Message and ex.StackTrace (two LogError calls)
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_ReturnsFailure_WhenStationServiceThrows()
//        {
//            var data = new QueryStringData
//            {
//                pollutantName = "NO2",
//                dataSource = "AURN",
//                Year = "2024",
//                Region = "London",
//                regiontype = "governmentRegion",
//                dataselectorfiltertype = "dataSelectorCount",
//                dataselectordownloadtype = "dataSelectorSingle",
//                email = "test@example.com"
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ThrowsAsync(new Exception("station service error"));

//            var result = await _service.GetatomDataSelectiondata(data);

//            Assert.Equal("Failure", result);
//        }

//        // ----------------------------------------------------------------
//        // Verify both LogError calls fire when an exception is caught
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_LogsTwoErrors_WhenStationServiceThrows()
//        {
//            var data = new QueryStringData
//            {
//                pollutantName = "O3",
//                dataSource = "AURN",
//                Year = "2021",
//                Region = "Wales",
//                regiontype = "country",
//                dataselectorfiltertype = "dataSelectorCount",
//                dataselectordownloadtype = "dataSelectorSingle",
//                email = "log@test.com"
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ThrowsAsync(new InvalidOperationException("critical failure"));

//            await _service.GetatomDataSelectiondata(data);

//            _loggerMock.Verify(
//                x => x.Log(
//                    LogLevel.Error,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, t) => true),
//                    It.IsAny<Exception>(),
//                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//                Times.Exactly(2));
//        }

//        // ----------------------------------------------------------------
//        // Correct field mapping: verify station service is called with the
//        // exact values extracted from QueryStringData
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_PassesCorrectFieldsToStationService()
//        {
//            var data = new QueryStringData
//            {
//                pollutantName = "CO",
//                dataSource = "NETWORK_X",
//                Year = "2020,2021",
//                Region = "Scotland",
//                regiontype = "country",
//                dataselectorfiltertype = "dataSelectorHourly",
//                dataselectordownloadtype = "dataSelectorMultiple",
//                email = "verify@mapping.com"
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    "CO", "NETWORK_X", "2020,2021", "Scotland",
//                    "country", "dataSelectorHourly",
//                    "dataSelectorMultiple", "verify@mapping.com"))
//                .ReturnsAsync("presigned-url")
//                .Verifiable();

//            var result = await _service.GetatomDataSelectiondata(data);

//            _atomDataSelectionStationServiceMock.Verify();
//            Assert.Equal("presigned-url", result);
//        }

//        // ----------------------------------------------------------------
//        // Station service returns an empty string — passes through as-is
//        // ----------------------------------------------------------------
//        [Fact]
//        public async Task GetatomDataSelectiondata_ReturnsEmptyString_WhenStationServiceReturnsEmpty()
//        {
//            var data = new QueryStringData
//            {
//                pollutantName = "NOx",
//                dataSource = "AURN",
//                Year = "2019",
//                Region = "East Midlands",
//                regiontype = "governmentRegion",
//                dataselectorfiltertype = "dataSelectorCount",
//                dataselectordownloadtype = "dataSelectorSingle",
//                email = "empty@test.com"
//            };

//            _atomDataSelectionStationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(string.Empty);

//            var result = await _service.GetatomDataSelectiondata(data);

//            Assert.Equal(string.Empty, result);
//        }
//    }
//}