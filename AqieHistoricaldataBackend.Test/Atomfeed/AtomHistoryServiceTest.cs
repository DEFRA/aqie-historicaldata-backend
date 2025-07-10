using Xunit;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Utils.Http;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Http.Headers;
using AtomModel = AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

public class AtomHistoryServiceTests
{
    private readonly Mock<ILogger<AtomHistoryService>> _loggerMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<IAtomHourlyFetchService> _hourlyServiceMock = new();
    private readonly Mock<IAtomDailyFetchService> _dailyServiceMock = new();
    private readonly Mock<IAtomAnnualFetchService> _annualServiceMock = new();
    private readonly Mock<IAWSS3BucketService> _s3ServiceMock = new();
    private readonly Mock<IHistoryexceedenceService> _exceedenceServiceMock = new();

    private AtomHistoryService CreateService(HttpClient client = null)
    {
        if (client != null)
        {
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
        }

        return new AtomHistoryService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _hourlyServiceMock.Object,
            _dailyServiceMock.Object,
            _annualServiceMock.Object,
            _s3ServiceMock.Object,
            _exceedenceServiceMock.Object
        );
    }

    [Fact]
    public async Task AtomHealthcheck_ReturnsError_WhenExceptionThrown()
    {
        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new Exception("fail"));
        var service = CreateService();

        var result = await service.AtomHealthcheck();

        Assert.Equal("Error", result);
    }

    [Fact]
    public async Task GetAtomHourlydata_ReturnsHourlyUrl()
    {
        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Hourly" };
        var finalData = new List<AtomHistoryModel.FinalData> { new AtomHistoryModel.FinalData() };

        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(finalData);
        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(finalData, data, "Hourly")).ReturnsAsync("url");

        var service = CreateService();
        var result = await service.GetAtomHourlydata(data);

        Assert.Equal("url", result);
    }

    [Fact]
    public async Task GetAtomHourlydata_ReturnsDailyUrl()
    {
        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Daily" };
        var hourlyData = new List<AtomHistoryModel.FinalData> { new AtomHistoryModel.FinalData() };
        var dailyData = new List<AtomHistoryModel.FinalData> { new AtomHistoryModel.FinalData() };

        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(hourlyData);
        _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(hourlyData, data)).ReturnsAsync(dailyData);
        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(dailyData, data, "Daily")).ReturnsAsync("daily-url");

        var service = CreateService();
        var result = await service.GetAtomHourlydata(data);

        Assert.Equal("daily-url", result);
    }

    [Fact]
    public async Task GetAtomHourlydata_ReturnsAnnualUrl()
    {
        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Annual" };
        var hourlyData = new List<AtomHistoryModel.FinalData> { new AtomHistoryModel.FinalData() };
        var annualData = new List<AtomHistoryModel.FinalData> { new AtomHistoryModel.FinalData() };

        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(hourlyData);
        _annualServiceMock.Setup(s => s.GetAtomAnnualdatafetch(hourlyData, data)).ReturnsAsync(annualData);
        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(annualData, data, "Annual")).ReturnsAsync("annual-url");

        var service = CreateService();
        var result = await service.GetAtomHourlydata(data);

        Assert.Equal("annual-url", result);
    }

    [Fact]
    public async Task GetAtomHourlydata_ReturnsEmpty_WhenExceptionThrown()
    {
        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Hourly" };
        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("fail"));

        var service = CreateService();
        var result = await service.GetAtomHourlydata(data);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void CallApi_LogsSuccess_WhenSuccessful()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Success")
            });

        var client = new HttpClient(handlerMock.Object);
        var service = CreateService(client);

        service.CallApi();

        _loggerMock.Verify(
                     x => x.Log(
                     LogLevel.Error,
                     It.IsAny<EventId>(),
                     It.Is<It.IsAnyType>((v, t) => true),
                     It.IsAny<Exception>(),
                     It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                     Times.AtLeastOnce);
    }

    [Fact]
    public void CallApi_LogsError_WhenFailed()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Error")
            });

        var client = new HttpClient(handlerMock.Object);
        var service = CreateService(client);

        service.CallApi();

        _loggerMock.Verify(
                     x => x.Log(
                     LogLevel.Error,
                     It.IsAny<EventId>(),
                     It.Is<It.IsAnyType>((v, t) => true),
                     It.IsAny<Exception>(),
                     It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                     Times.AtLeastOnce);
    }

    [Fact]
    public void CallApi_HandlesHttpRequestException()
    {
        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new HttpRequestException("fail"));
        var service = CreateService();

        service.CallApi();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }


    [Fact]
    public void CallApi_HandlesGeneralException()
    {
        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new Exception("fail"));
        var service = CreateService();

        service.CallApi();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }


    [Fact]
    public async Task GetHistoryexceedencedata_ReturnsData_WhenSuccessful()
    {
        var data = new AtomModel.QueryStringData();
        _exceedenceServiceMock.Setup(s => s.GetHistoryexceedencedata(data)).ReturnsAsync("result");

        var service = CreateService();
        var result = await service.GetHistoryexceedencedata(data);

        Assert.Equal("result", result);
    }

    [Fact]
    public async Task GetHistoryexceedencedata_ReturnsFailure_WhenExceptionThrown()
    {
        var data = new AtomModel.QueryStringData();
        _exceedenceServiceMock.Setup(s => s.GetHistoryexceedencedata(data)).ThrowsAsync(new Exception("fail"));

        var service = CreateService();
        var result = await service.GetHistoryexceedencedata(data);

        Assert.Equal("Failure", result);
    }
}
