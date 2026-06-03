using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AqieHistoricaldataBackend.Atomfeed.Services;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionServiceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IAtomDataSelectionStationService> _stationServiceMock;
        private readonly AtomDataSelectionService _service;

        public AtomDataSelectionServiceTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _stationServiceMock = new Mock<IAtomDataSelectionStationService>();
            _service = new AtomDataSelectionService(_loggerMock.Object, _stationServiceMock.Object);
        }

        // ----------------------------------------------------------------
        // Happy path — try branch: station service result is returned as-is
        // ----------------------------------------------------------------
        [Theory]
        [InlineData("NO2", "NET1", "AURN", "2024", "London", "governmentRegion", "dataSelectorCount", "dataSelectorSingle", "a@b.com", "42")]
        [InlineData(null,  null,   null,   null,   null,     null,                null,                null,                 null,      "0")]
        [InlineData("PM10","NET2", "AURN", "2023", "Wales",  "country",           "dataSelectorHourly","dataSelectorMultiple","x@y.com", "")]
        public async Task GetatomDataSelectiondata_ReturnStationResult_OnSuccess(
            string? pollutant, string? networkId, string? source, string? year,
            string? region, string? regiontype, string? filtertype, string? dltype,
            string? email, string stationReturn)
        {
            var data = new QueryStringData
            {
                pollutantName            = pollutant,
                networkId                = networkId,
                dataSource               = source,
                Year                     = year,
                Region                   = region,
                regiontype               = regiontype,
                dataselectorfiltertype   = filtertype,
                dataselectordownloadtype = dltype,
                email                    = email
            };

            _stationServiceMock
                .Setup(s => s.GetAtomDataSelectionStation(
                    pollutant, networkId, source, year,
                    region, regiontype, filtertype, dltype, email))
                .ReturnsAsync(stationReturn);

            var result = await _service.GetatomDataSelectiondata(data);

            Assert.Equal(stationReturn, result);
        }

        // ----------------------------------------------------------------
        // Field mapping — verify exact values are forwarded to the station
        // service (covers every local variable assignment)
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetatomDataSelectiondata_ForwardsAllFieldsCorrectly()
        {
            var data = new QueryStringData
            {
                pollutantName            = "CO",
                networkId                = "NET_X",
                dataSource               = "AURN",
                Year                     = "2020",
                Region                   = "Scotland",
                regiontype               = "country",
                dataselectorfiltertype   = "dataSelectorHourly",
                dataselectordownloadtype = "dataSelectorMultiple",
                email                    = "verify@mapping.com"
            };

            _stationServiceMock
                .Setup(s => s.GetAtomDataSelectionStation(
                    "CO", "NET_X", "AURN", "2020",
                    "Scotland", "country",
                    "dataSelectorHourly", "dataSelectorMultiple",
                    "verify@mapping.com"))
                .ReturnsAsync("ok")
                .Verifiable();

            await _service.GetatomDataSelectiondata(data);

            _stationServiceMock.Verify();
        }

        // ----------------------------------------------------------------
        // Catch branch — exception returns "Failure"
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetatomDataSelectiondata_ReturnsFailure_WhenStationServiceThrows()
        {
            _stationServiceMock
                .Setup(s => s.GetAtomDataSelectionStation(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("station error"));

            var result = await _service.GetatomDataSelectiondata(new QueryStringData());

            Assert.Equal("Failure", result);
        }

        // ----------------------------------------------------------------
        // Catch branch — LogError is called exactly once with the exception
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetatomDataSelectiondata_LogsError_WhenStationServiceThrows()
        {
            var exception = new InvalidOperationException("critical failure");

            _stationServiceMock
                .Setup(s => s.GetAtomDataSelectionStation(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(exception);

            await _service.GetatomDataSelectiondata(new QueryStringData());

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Atom atomDataSelectiondata")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}