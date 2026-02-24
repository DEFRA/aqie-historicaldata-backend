//using Xunit;
//using Moq;
//using Moq.Protected;
//using System.Net;
//using System.Net.Http;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
//using AqieHistoricaldataBackend.Atomfeed.Services;
//using AqieHistoricaldataBackend.Atomfeed.Models;
//using AqieHistoricaldataBackend.Utils.Http;
//using System;
//using System.Collections.Generic;
//using System.Threading;
//using AtomModel = AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
//using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

//public class AtomHistoryServiceTests
//{
//    private readonly Mock<ILogger<AtomHistoryService>> _loggerMock = new();
//    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
//    private readonly Mock<IAtomHourlyFetchService> _hourlyServiceMock = new();
//    private readonly Mock<IAtomDailyFetchService> _dailyServiceMock = new();
//    private readonly Mock<IAtomAnnualFetchService> _annualServiceMock = new();
//    private readonly Mock<IAWSS3BucketService> _s3ServiceMock = new();
//    private readonly Mock<IHistoryexceedenceService> _exceedenceServiceMock = new();
//    private readonly Mock<IAtomDataSelectionService> _dataSelectionServiceMock = new();
//    private readonly Mock<IAtomDataSelectionJobStatus> _dataSelectionJobStatusMock = new();
//    private readonly Mock<IAtomDataSelectionEmailJobService> _dataSelectionEmailJobServiceMock = new();

//    private AtomHistoryService CreateService(HttpClient client = null)
//    {
//        if (client != null)
//        {
//            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
//        }

//        return new AtomHistoryService(
//            _loggerMock.Object,
//            _httpClientFactoryMock.Object,
//            _hourlyServiceMock.Object,
//            _dailyServiceMock.Object,
//            _annualServiceMock.Object,
//            _s3ServiceMock.Object,
//            _dataSelectionServiceMock.Object,
//            _dataSelectionJobStatusMock.Object,
//            _dataSelectionEmailJobServiceMock.Object,
//            _exceedenceServiceMock.Object
//        );
//    }

//    private static HttpClient BuildHttpClient(HttpStatusCode statusCode, string content = "")
//    {
//        var handlerMock = new Mock<HttpMessageHandler>();
//        handlerMock.Protected()
//            .Setup<Task<HttpResponseMessage>>(
//                "SendAsync",
//                ItExpr.IsAny<HttpRequestMessage>(),
//                ItExpr.IsAny<CancellationToken>())
//            .ReturnsAsync(new HttpResponseMessage
//            {
//                StatusCode = statusCode,
//                Content = new StringContent(content)
//            });

//        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
//    }

//    [Fact]
//    public async Task AtomHealthcheck_ReturnsError_WhenHttpFails()
//    {
//        var client = BuildHttpClient(HttpStatusCode.InternalServerError);
//        var service = CreateService(client);

//        var result = await service.AtomHealthcheck();

//        Assert.Equal("Error", result);
//    }

//    [Fact]
//    public async Task AtomHealthcheck_ReturnsError_WhenExceptionThrown()
//    {
//        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new Exception("fail"));
//        var service = CreateService();

//        var result = await service.AtomHealthcheck();

//        Assert.Equal("Error", result);
//    }

//    [Fact]
//    public async Task AtomHealthcheck_LogsError_WhenExceptionThrown()
//    {
//        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new Exception("fail"));
//        var service = CreateService();

//        await service.AtomHealthcheck();

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    // ─── GetAtomHourlydata ────────────────────────────────────────────────────

//    [Fact]
//    public async Task GetAtomHourlydata_ReturnsHourlyUrl_WhenTypeIsHourly()
//    {
//        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Hourly" };
//        var hourlyData = new List<AtomHistoryModel.FinalData> { new() };

//        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(hourlyData);
//        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(hourlyData, data, "Hourly")).ReturnsAsync("hourly-url");

//        var service = CreateService();
//        var result = await service.GetAtomHourlydata(data);

//        Assert.Equal("hourly-url", result);
//    }

//    [Fact]
//    public async Task GetAtomHourlydata_ReturnsDailyUrl_WhenTypeIsDaily()
//    {
//        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Daily" };
//        var hourlyData = new List<AtomHistoryModel.FinalData> { new() };
//        var dailyData = new List<AtomHistoryModel.FinalData> { new() };

//        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(hourlyData);
//        _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(hourlyData, data)).ReturnsAsync(dailyData);
//        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(dailyData, data, "Daily")).ReturnsAsync("daily-url");

//        var service = CreateService();
//        var result = await service.GetAtomHourlydata(data);

//        Assert.Equal("daily-url", result);
//    }

//    [Fact]
//    public async Task GetAtomHourlydata_ReturnsAnnualUrl_WhenTypeIsAnnual()
//    {
//        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Annual" };
//        var hourlyData = new List<AtomHistoryModel.FinalData> { new() };
//        var annualData = new List<AtomHistoryModel.FinalData> { new() };

//        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(hourlyData);
//        _annualServiceMock.Setup(s => s.GetAtomAnnualdatafetch(hourlyData, data)).ReturnsAsync(annualData);
//        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(annualData, data, "Annual")).ReturnsAsync("annual-url");

//        var service = CreateService();
//        var result = await service.GetAtomHourlydata(data);

//        Assert.Equal("annual-url", result);
//    }

//    [Fact]
//    public async Task GetAtomHourlydata_ReturnsEmpty_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Hourly" };

//        _hourlyServiceMock
//            .Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//            .ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        var result = await service.GetAtomHourlydata(data);

//        Assert.Equal(string.Empty, result);
//    }

//    [Fact]
//    public async Task GetAtomHourlydata_LogsError_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Hourly" };

//        _hourlyServiceMock
//            .Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//            .ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        await service.GetAtomHourlydata(data);

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    [Fact]
//    public async Task GetAtomHourlydata_ReturnsEmpty_WhenS3ThrowsOnDaily()
//    {
//        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Daily" };
//        var hourlyData = new List<AtomHistoryModel.FinalData> { new() };
//        var dailyData = new List<AtomHistoryModel.FinalData> { new() };

//        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(hourlyData);
//        _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(hourlyData, data)).ReturnsAsync(dailyData);
//        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(dailyData, data, "Daily")).ThrowsAsync(new Exception("s3 fail"));

//        var service = CreateService();
//        var result = await service.GetAtomHourlydata(data);

//        Assert.Equal(string.Empty, result);
//    }

//    [Fact]
//    public async Task GetAtomHourlydata_ReturnsEmpty_WhenS3ThrowsOnAnnual()
//    {
//        var data = new AtomModel.QueryStringData { SiteId = "site", Year = "2023", DownloadPollutant = "NO2", DownloadPollutantType = "Annual" };
//        var hourlyData = new List<AtomHistoryModel.FinalData> { new() };
//        var annualData = new List<AtomHistoryModel.FinalData> { new() };

//        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site", "2023", "NO2")).ReturnsAsync(hourlyData);
//        _annualServiceMock.Setup(s => s.GetAtomAnnualdatafetch(hourlyData, data)).ReturnsAsync(annualData);
//        _s3ServiceMock.Setup(s => s.writecsvtoawss3bucket(annualData, data, "Annual")).ThrowsAsync(new Exception("s3 fail"));

//        var service = CreateService();
//        var result = await service.GetAtomHourlydata(data);

//        Assert.Equal(string.Empty, result);
//    }

//    // ─── CallApi ─────────────────────────────────────────────────────────────

//    [Fact]
//    public void CallApi_DoesNotLog_WhenHttpSucceeds()
//    {
//        var client = BuildHttpClient(HttpStatusCode.OK, "Success");
//        var service = CreateService(client);

//        service.CallApi();

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.Never);
//    }

//    [Fact]
//    public void CallApi_LogsError_WhenHttpFails()
//    {
//        var client = BuildHttpClient(HttpStatusCode.InternalServerError);
//        var service = CreateService(client);

//        service.CallApi();

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    [Fact]
//    public void CallApi_LogsError_WhenHttpRequestExceptionThrown()
//    {
//        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new HttpRequestException("fail"));
//        var service = CreateService();

//        service.CallApi();

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    [Fact]
//    public void CallApi_LogsError_WhenGeneralExceptionThrown()
//    {
//        _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Throws(new Exception("fail"));
//        var service = CreateService();

//        service.CallApi();

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    // ─── GetHistoryexceedencedata ─────────────────────────────────────────────

//    [Fact]
//    public async Task GetHistoryexceedencedata_ReturnsData_WhenSuccessful()
//    {
//        var data = new AtomModel.QueryStringData();
//        _exceedenceServiceMock.Setup(s => s.GetHistoryexceedencedata(data)).ReturnsAsync("result");

//        var service = CreateService();
//        var result = await service.GetHistoryexceedencedata(data);

//        Assert.Equal("result", result);
//    }

//    [Fact]
//    public async Task GetHistoryexceedencedata_ReturnsFailure_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData();
//        _exceedenceServiceMock.Setup(s => s.GetHistoryexceedencedata(data)).ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        var result = await service.GetHistoryexceedencedata(data);

//        Assert.Equal("Failure", result);
//    }

//    [Fact]
//    public async Task GetHistoryexceedencedata_LogsError_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData();
//        _exceedenceServiceMock.Setup(s => s.GetHistoryexceedencedata(data)).ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        await service.GetHistoryexceedencedata(data);

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    // ─── GetatomDataSelectiondata ─────────────────────────────────────────────

//    [Fact]
//    public async Task GetatomDataSelectiondata_ReturnsData_WhenSuccessful()
//    {
//        var data = new AtomModel.QueryStringData();
//        _dataSelectionServiceMock.Setup(s => s.GetatomDataSelectiondata(data)).ReturnsAsync("selection-result");

//        var service = CreateService();
//        var result = await service.GetatomDataSelectiondata(data);

//        Assert.Equal("selection-result", result);
//    }

//    [Fact]
//    public async Task GetatomDataSelectiondata_ReturnsFailure_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData();
//        _dataSelectionServiceMock.Setup(s => s.GetatomDataSelectiondata(data)).ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        var result = await service.GetatomDataSelectiondata(data);

//        Assert.Equal("Failure", result);
//    }

//    [Fact]
//    public async Task GetatomDataSelectiondata_LogsError_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData();
//        _dataSelectionServiceMock.Setup(s => s.GetatomDataSelectiondata(data)).ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        await service.GetatomDataSelectiondata(data);

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    // ─── GetAtomDataSelectionJobStatusdata ────────────────────────────────────

//    [Fact]
//    public async Task GetAtomDataSelectionJobStatusdata_ReturnsJobInfo_WhenSuccessful()
//    {
//        var data = new AtomModel.QueryStringData { jobId = "job-123" };
//        var jobInfo = new JobInfoDto { JobId = "job-123", Status = "Completed" };

//        _dataSelectionJobStatusMock
//            .Setup(s => s.GetAtomDataSelectionJobStatusdata("job-123"))
//            .ReturnsAsync(jobInfo);

//        var service = CreateService();
//        var result = await service.GetAtomDataSelectionJobStatusdata(data);

//        Assert.NotNull(result);
//        Assert.Equal("job-123", result.JobId);
//        Assert.Equal("Completed", result.Status);
//    }

//    [Fact]
//    public async Task GetAtomDataSelectionJobStatusdata_ReturnsDefault_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData { jobId = "job-123" };

//        _dataSelectionJobStatusMock
//            .Setup(s => s.GetAtomDataSelectionJobStatusdata("job-123"))
//            .ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        var result = await service.GetAtomDataSelectionJobStatusdata(data);

//        Assert.Null(result);
//    }

//    [Fact]
//    public async Task GetAtomDataSelectionJobStatusdata_LogsError_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData { jobId = "job-123" };

//        _dataSelectionJobStatusMock
//            .Setup(s => s.GetAtomDataSelectionJobStatusdata("job-123"))
//            .ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        await service.GetAtomDataSelectionJobStatusdata(data);

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }

//    [Fact]
//    public async Task GetAtomDataSelectionJobStatusdata_ReturnsNull_WhenJobIdIsNull()
//    {
//        var data = new AtomModel.QueryStringData { jobId = null };

//        _dataSelectionJobStatusMock
//            .Setup(s => s.GetAtomDataSelectionJobStatusdata(null))
//            .ReturnsAsync((JobInfoDto)null);

//        var service = CreateService();
//        var result = await service.GetAtomDataSelectionJobStatusdata(data);

//        Assert.Null(result);
//    }

//    // ─── GetAtomemailjobDataSelection ─────────────────────────────────────────

//    [Fact]
//    public async Task GetAtomemailjobDataSelection_ReturnsData_WhenSuccessful()
//    {
//        var data = new AtomModel.QueryStringData { email = "test@test.com" };
//        _dataSelectionEmailJobServiceMock
//            .Setup(s => s.GetAtomemailjobDataSelection(data))
//            .ReturnsAsync("email-result");

//        var service = CreateService();
//        var result = await service.GetAtomemailjobDataSelection(data);

//        Assert.Equal("email-result", result);
//    }

//    [Fact]
//    public async Task GetAtomemailjobDataSelection_ReturnsFailure_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData { email = "test@test.com" };
//        _dataSelectionEmailJobServiceMock
//            .Setup(s => s.GetAtomemailjobDataSelection(data))
//            .ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        var result = await service.GetAtomemailjobDataSelection(data);

//        Assert.Equal("Failure", result);
//    }

//    [Fact]
//    public async Task GetAtomemailjobDataSelection_LogsError_WhenExceptionThrown()
//    {
//        var data = new AtomModel.QueryStringData { email = "test@test.com" };
//        _dataSelectionEmailJobServiceMock
//            .Setup(s => s.GetAtomemailjobDataSelection(data))
//            .ThrowsAsync(new Exception("fail"));

//        var service = CreateService();
//        await service.GetAtomemailjobDataSelection(data);

//        _loggerMock.Verify(
//            x => x.Log(
//                LogLevel.Error,
//                It.IsAny<EventId>(),
//                It.Is<It.IsAnyType>((v, t) => true),
//                It.IsAny<Exception>(),
//                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//            Times.AtLeastOnce);
//    }
//}
