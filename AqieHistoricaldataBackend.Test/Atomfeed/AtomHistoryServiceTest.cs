using Xunit;
using Moq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Utils.Http;
using System.Collections.Generic;
using Moq.Protected;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

public class AtomHistoryServiceTests
{
//    private readonly Mock<ILogger<AtomHistoryService>> _loggerMock = new();
//    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
//    private readonly Mock<IAtomHourlyFetchService> _hourlyServiceMock = new();
//    private readonly Mock<IAtomDailyFetchService> _dailyServiceMock = new();
//    private readonly Mock<IAtomAnnualFetchService> _annualServiceMock = new();
//    private readonly Mock<IAWSS3BucketService> _s3ServiceMock = new();
//    private readonly Mock<IHistoryexceedenceService> _exceedenceServiceMock = new();

//    private readonly AtomHistoryService _service;

//    public AtomHistoryServiceTests()
//    {
//        _service = new AtomHistoryService(
//            _loggerMock.Object,
//            _httpClientFactoryMock.Object,
//            _hourlyServiceMock.Object,
//            _dailyServiceMock.Object,
//            _annualServiceMock.Object,
//            _s3ServiceMock.Object,
//            _exceedenceServiceMock.Object
//        );
//    }

//    [Fact]
//    public async Task AtomHealthcheck_ReturnsResponseString_OnSuccess()
//    {
//        var handlerMock = new Mock<HttpMessageHandler>();
//        handlerMock.Protected()
//            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
//            .ReturnsAsync(new HttpResponseMessage
//            {
//                StatusCode = HttpStatusCode.OK,
//                Content = new StringContent("Success")
//            });

//        var client = new HttpClient(handlerMock.Object);
//        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

//        var result = await _service.AtomHealthcheck();

//        Assert.Contains("StatusCode", result);
//    }

//    ("Atomfeed")).Throws(new HttpRequestException("Failure"));

//        var result = await _service.AtomHealthcheck();

//    Assert.Equal("Error", result);
//    }

//[Fact]
//public async Task GetAtomHourlydata_ReturnsPresignedUrl_ForHourly()
//{
//    var data = new querystringdata { siteId = "123", year = "2023", downloadpollutant = "NO2", downloadpollutanttype = "Hourly" };
//    var hourlyData = new List<string> { "data" };

//    _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("123", "2023", "NO2")).ReturnsAsync(hourlyData);
//    _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(hourlyData, data, "Hourly")).ReturnsAsync("url");

//    var result = await _service.GetAtomHourlydata(data);

//    Assert.Equal("url", result);
//}

//[Fact]
//public async Task GetAtomHourlydata_ReturnsPresignedUrl_ForDaily()
//{
//    var data = new querystringdata { siteId = "123", year = "2023", downloadpollutant = "NO2", downloadpollutanttype = "Daily" };
//    var hourlyData = new List<string> { "data" };
//    var dailyData = new List<string> { "daily" };

//    _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("123", "2023", "NO2")).ReturnsAsync(hourlyData);
//    _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(hourlyData, data)).ReturnsAsync(dailyData);
//    _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(dailyData, data, "Daily")).ReturnsAsync("url");

//    var result = await _service.GetAtomHourlydata(data);

//    Assert.Equal("url", result);
//}

//[Fact]
//public async Task GetAtomHourlydata_ReturnsPresignedUrl_ForAnnual()
//{
//    var data = new querystringdata { siteId = "123", year = "2023", downloadpollutant = "NO2", downloadpollutanttype = "Annual" };
//    var hourlyData = new List<string> { "data" };
//    var annualData = new List<string> { "annual" };

//    _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("123", "2023", "NO2")).ReturnsAsync(hourlyData);
//    _annualServiceMock.Setup(s => s.GetAtomAnnualdatafetch(hourlyData, data)).ReturnsAsync(annualData);
//    _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(annualData, data, "Annual")).ReturnsAsync("url");

//    var result = await _service.GetAtomHourlydata(data);

//    Assert.Equal("url", result);
//}

//[Fact]
//public async Task GetAtomHourlydata_ReturnsEmpty_OnException()
//{
//    var data = new querystringdata { siteId = "123", year = "2023", downloadpollutant = "NO2", downloadpollutanttype = "Hourly" };

//    _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//        .ThrowsAsync(new Exception("Failure"));

//    var result = await _service.GetAtomHourlydata(data);

//    Assert.Equal(string.Empty, result);
//}

//[Fact]
//public void CallApi_LogsSuccess_OnValidResponse()
//{
//    var handlerMock = new Mock<HttpMessageHandler>();
//    handlerMock.Protected()
//        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
//        .ReturnsAsync(new HttpResponseMessage
//        {
//            StatusCode = HttpStatusCode.OK,
//            Content = new StringContent("Success")
//        });

//    var client = new HttpClient(handlerMock.Object);
//    _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

//    _service.CallApi();

//    _loggerMock.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
//}

//[Fact]
//public void CallApi_HandlesHttpRequestException()
//{
//    _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new HttpRequestException("HTTP error"));

//    _service.CallApi();

//    _loggerMock.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
//}

//[Fact]
//public async Task GetHistoryexceedencedata_ReturnsData_OnSuccess()
//{
//    var data = new querystringdata();
//    var expected = new List<string> { "exceedance" };

//    _exceedenceServiceMock.Setup(s => s.GetHistoryexceedencedata(data)).ReturnsAsync(expected);

//    var result = await _service.GetHistoryexceedencedata(data);

//    Assert.Equal(expected, result);
//}

//[Fact]
//public async Task GetHistoryexceedencedata_ReturnsFailure_OnException()
//{
//    var data = new querystringdata();

//    _exceedenceServiceMock.Setup(s => s.GetHistoryexceedencedata(data)).ThrowsAsync(new Exception("Error"));

//    var result = await _service.GetHistoryexceedencedata(data);

//    Assert.Equal("Failure", result);
//}
}
