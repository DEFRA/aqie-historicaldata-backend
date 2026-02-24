//using System;
//using System.Collections.Generic;
//using System.Net;
//using System.Net.Http;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using AqieHistoricaldataBackend.Atomfeed.Services;
//using AqieHistoricaldataBackend.Utils.Mongo;
//using Microsoft.Extensions.Logging;
//using Moq;
//using Moq.Protected;
//using MongoDB.Driver;
//using Xunit;
//using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

//namespace AqieHistoricaldataBackend.Test.Atomfeed
//{
//    public class AuthServiceTests
//    {
//        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
//        private readonly Mock<HttpMessageHandler> _handlerMock = new();

//        private AuthService CreateService()
//        {
//            var client = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri("https://api.test/") };
//            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);
//            return new AuthService(_httpClientFactoryMock.Object);
//        }

//        private void SetupResponse(HttpStatusCode status, string body)
//        {
//            _handlerMock.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync",
//                    ItExpr.IsAny<HttpRequestMessage>(),
//                    ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = status,
//                    Content = new StringContent(body, Encoding.UTF8, "application/json")
//                });
//        }

//        // Returns null when HTTP response is not successful
//        [Fact]
//        public async Task GetTokenAsync_ReturnsNull_WhenResponseNotSuccessful()
//        {
//            SetupResponse(HttpStatusCode.Unauthorized, "{}");
//            var service = CreateService();

//            var result = await service.GetTokenAsync("user@test.com", "pass");

//            Assert.Null(result);
//        }

//        // Extracts token from "token" property
//        [Fact]
//        public async Task GetTokenAsync_ReturnsToken_FromTokenProperty()
//        {
//            SetupResponse(HttpStatusCode.OK, "{\"token\": \"abc123\"}");
//            var result = await CreateService().GetTokenAsync("u", "p");

//            Assert.Equal("abc123", result);
//        }

//        // Extracts token from "access_token" property
//        [Fact]
//        public async Task GetTokenAsync_ReturnsToken_FromAccessTokenProperty()
//        {
//            SetupResponse(HttpStatusCode.OK, "{\"access_token\": \"tok_access\"}");
//            var result = await CreateService().GetTokenAsync("u", "p");

//            Assert.Equal("tok_access", result);
//        }

//        // Extracts token from "jwt" property
//        [Fact]
//        public async Task GetTokenAsync_ReturnsToken_FromJwtProperty()
//        {
//            SetupResponse(HttpStatusCode.OK, "{\"jwt\": \"jwt_value\"}");
//            var result = await CreateService().GetTokenAsync("u", "p");

//            Assert.Equal("jwt_value", result);
//        }

//        // Returns string value when root JSON element is a plain string
//        [Fact]
//        public async Task GetTokenAsync_ReturnsToken_WhenRootIsString()
//        {
//            SetupResponse(HttpStatusCode.OK, "\"plain_token\"");
//            var result = await CreateService().GetTokenAsync("u", "p");

//            Assert.Equal("plain_token", result);
//        }

//        // Returns null when JSON has no known token property and root is not a string
//        [Fact]
//        public async Task GetTokenAsync_ReturnsNull_WhenNoKnownTokenProperty()
//        {
//            SetupResponse(HttpStatusCode.OK, "{\"unknown_key\": \"value\"}");
//            var result = await CreateService().GetTokenAsync("u", "p");

//            Assert.Null(result);
//        }
//    }

//    public class AtomDataSelectionStationServiceTests
//    {
//        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock = new();
//        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
//        private readonly Mock<IAtomDataSelectionStationBoundryService> _boundaryServiceMock = new();
//        private readonly Mock<IAtomDataSelectionLocalAuthoritiesService> _localAuthMock = new();
//        private readonly Mock<IAtomDataSelectionHourlyFetchService> _hourlyFetchMock = new();
//        private readonly Mock<IAWSS3BucketService> _s3Mock = new();
//        private readonly Mock<IAuthService> _authServiceMock = new();
//        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock = new();
//        private readonly Mock<HttpMessageHandler> _ricardoHandlerMock = new();
//        private readonly Mock<HttpMessageHandler> _siteMetaHandlerMock = new();

//        // ------------------------------------------------------------------
//        // Helpers
//        // ------------------------------------------------------------------

//        private AtomDataSelectionStationService CreateService()
//        {
//            return new AtomDataSelectionStationService(
//                _loggerMock.Object,
//                _httpClientFactoryMock.Object,
//                _boundaryServiceMock.Object,
//                _localAuthMock.Object,
//                _hourlyFetchMock.Object,
//                _s3Mock.Object,
//                _authServiceMock.Object,
//                _mongoFactoryMock.Object);
//        }

//        /// <summary>Build a minimal valid site-meta JSON response.</summary>
//        private static string BuildSiteMetaJson(
//            string localSiteId = "SITE001",
//            string siteName = "Test Site",
//            string pollutantName = "Nitrogen dioxide",
//            string startDate = "01/01/2023",
//            string endDate = "31/12/2023")
//        {
//            return JsonSerializer.Serialize(new
//            {
//                member = new[]
//                {
//                    new
//                    {
//                        siteName,
//                        localSiteId,
//                        areaType = "Urban",
//                        siteType = "Background",
//                        governmentRegion = "London",
//                        latitude = "51.5074",
//                        longitude = "-0.1278",
//                        pollutantsMetaData = new Dictionary<string, object>
//                        {
//                            ["NO2"] = new { name = pollutantName, startDate, endDate }
//                        }
//                    }
//                }
//            });
//        }

//        private void SetupHttpFactory(string siteMetaJson, string token = "valid_token")
//        {
//            // Auth token mock
//            _authServiceMock
//                .Setup(a => a.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(token);

//            // Site meta HTTP response
//            _siteMetaHandlerMock.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync",
//                    ItExpr.IsAny<HttpRequestMessage>(),
//                    ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage
//                {
//                    StatusCode = HttpStatusCode.OK,
//                    Content = new StringContent(siteMetaJson, Encoding.UTF8, "application/json")
//                });

//            var client = new HttpClient(_siteMetaHandlerMock.Object)
//            {
//                BaseAddress = new Uri("https://api.test/")
//            };

//            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);
//        }

//        private void SetupMongoDB()
//        {
//            var collectionMock = new Mock<IMongoCollection<JobDocument>>();
//            var indexManagerMock = new Mock<IMongoIndexManager<JobDocument>>();

//            collectionMock.Setup(c => c.Indexes).Returns(indexManagerMock.Object);
//            indexManagerMock
//                .Setup(i => i.CreateOne(It.IsAny<CreateIndexModel<JobDocument>>(), null, default))
//                .Returns("index_name");

//            collectionMock
//                .Setup(c => c.InsertOneAsync(It.IsAny<JobDocument>(), null, default))
//                .Returns(Task.CompletedTask);

//            collectionMock
//                .Setup(c => c.UpdateOneAsync(
//                    It.IsAny<FilterDefinition<JobDocument>>(),
//                    It.IsAny<UpdateDefinition<JobDocument>>(),
//                    It.IsAny<UpdateOptions>(),
//                    default))
//                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

//            _mongoFactoryMock
//                .Setup(m => m.GetCollection<JobDocument>(It.IsAny<string>()))
//                .Returns(collectionMock.Object);
//        }

//        // ------------------------------------------------------------------
//        // dataSelectorCount branch
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsCount_WhenDataselectorCount()
//        {
//            var siteMetaJson = BuildSiteMetaJson();
//            SetupHttpFactory(siteMetaJson);

//            var sites = new List<SiteInfo>
//            {
//                new SiteInfo { LocalSiteId = "SITE001", Latitude = "51.5", Longitude = "-0.1" }
//            };

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(sites);

//            var service = CreateService();
//            var result = await service.GetAtomDataSelectionStation(
//                "NO2", "source", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "user@test.com");

//            Assert.Equal("1", result);
//        }

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsZeroCount_WhenNoBoundaryMatch()
//        {
//            SetupHttpFactory(BuildSiteMetaJson());

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo>());

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("0", result);
//        }

//        // ------------------------------------------------------------------
//        // dataSelectorHourly + dataSelectorMultiple (presigned URL) branch
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsPresignedUrl_WhenDataselectorMultiple()
//        {
//            SetupHttpFactory(BuildSiteMetaJson());

//            var stationData = new List<SiteInfo>
//            {
//                new SiteInfo { LocalSiteId = "SITE001" }
//            };

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(stationData);

//            _hourlyFetchMock
//                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<FinalData>());

//            _s3Mock
//                .Setup(s => s.writecsvtoawss3bucket(It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), It.IsAny<string>()))
//                .ReturnsAsync("https://s3.example.com/file.zip");

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorHourly", "dataSelectorMultiple", "u@t.com");

//            Assert.Equal("https://s3.example.com/file.zip", result);
//        }

//        // ------------------------------------------------------------------
//        // dataSelectorHourly + dataSelectorSingle (job-queue) branch
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsJobId_WhenDataselectorSingle()
//        {
//            SetupHttpFactory(BuildSiteMetaJson());
//            SetupMongoDB();

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorHourly", "dataSelectorSingle", "u@t.com");

//            // Job ID is a 32-char hex GUID (Guid.NewGuid().ToString("N"))
//            Assert.NotNull(result);
//            Assert.Equal(32, result.Length);
//            Assert.Matches("^[a-f0-9]{32}$", result);
//        }

//        // ------------------------------------------------------------------
//        // Token failure → returns "Failure"
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenTokenIsNull()
//        {
//            _authServiceMock
//                .Setup(a => a.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync((string?)null);

//            // Provide a stub HTTP client so the factory doesn't throw
//            var client = new HttpClient { BaseAddress = new Uri("https://api.test/") };
//            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("Failure", result);
//        }

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenTokenIsEmpty()
//        {
//            _authServiceMock
//                .Setup(a => a.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(string.Empty);

//            var client = new HttpClient { BaseAddress = new Uri("https://api.test/") };
//            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("Failure", result);
//        }

//        // ------------------------------------------------------------------
//        // HTTP failure on site-meta call → returns "Failure"
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenSiteMetaHttpFails()
//        {
//            _authServiceMock
//                .Setup(a => a.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync("valid_token");

//            _siteMetaHandlerMock.Protected()
//                .Setup<Task<HttpResponseMessage>>("SendAsync",
//                    ItExpr.IsAny<HttpRequestMessage>(),
//                    ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });

//            var client = new HttpClient(_siteMetaHandlerMock.Object)
//            {
//                BaseAddress = new Uri("https://api.test/")
//            };
//            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("Failure", result);
//        }

//        // ------------------------------------------------------------------
//        // Exception thrown anywhere → returns "Failure"
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenExceptionThrown()
//        {
//            _authServiceMock
//                .Setup(a => a.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
//                .ThrowsAsync(new Exception("unexpected"));

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("Failure", result);
//        }

//        // ------------------------------------------------------------------
//        // Unknown dataselectorfiltertype → returns "Failure"
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenUnknownFilterType()
//        {
//            SetupHttpFactory(BuildSiteMetaJson());

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo>());

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "unknownFilterType", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("Failure", result);
//        }

//        // ------------------------------------------------------------------
//        // Pollutant mapping: known pollutants are expanded to their full names
//        // ------------------------------------------------------------------

//        [Theory]
//        [InlineData("NO2", "Nitrogen dioxide")]
//        [InlineData("SO2", "Sulphur dioxide")]
//        [InlineData("CO", "Carbon monoxide")]
//        [InlineData("NOx", "Nitrogen oxides as nitrogen dioxide")]
//        [InlineData("NO", "Nitric oxide")]
//        [InlineData("Ozone", "Ozone")]
//        public async Task GetAtomDataSelectionStation_MapsKnownPollutants_Correctly(
//            string pollutantInput, string expectedMappedName)
//        {
//            // Build site meta with the mapped pollutant name
//            var siteMetaJson = BuildSiteMetaJson(pollutantName: expectedMappedName);
//            SetupHttpFactory(siteMetaJson);

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

//            var result = await CreateService().GetAtomDataSelectionStation(
//                pollutantInput, "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("1", result);
//        }

//        // ------------------------------------------------------------------
//        // Unknown pollutant name is added as-is (includeUnknowns: true)
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_IncludesUnknownPollutant_AsRawName()
//        {
//            var siteMetaJson = BuildSiteMetaJson(pollutantName: "SomeFuturePollutant");
//            SetupHttpFactory(siteMetaJson);

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "SomeFuturePollutant", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("1", result);
//        }

//        // ------------------------------------------------------------------
//        // PM2.5 → multiple mapped names are all accepted
//        // ------------------------------------------------------------------

//        [Theory]
//        [InlineData("PM<sub>2.5</sub> (Hourly measured)")]
//        [InlineData("Volatile PM<sub>2.5</sub> (Hourly measured)")]
//        [InlineData("Non-volatile PM<sub>2.5</sub> (Hourly measured)")]
//        [InlineData("PM<sub>2.5</sub> particulate matter (Hourly measured)")]
//        public async Task GetAtomDataSelectionStation_ReturnsCount_ForAllPm25MappedNames(string mappedName)
//        {
//            SetupHttpFactory(BuildSiteMetaJson(pollutantName: mappedName));

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "PM2.5", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("1", result);
//        }

//        // ------------------------------------------------------------------
//        // Year filtering: site whose pollutant dates do NOT overlap the
//        // requested year should be filtered out
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_FiltersOutSites_WhenPollutantYearDoesNotOverlap()
//        {
//            // Pollutant ended in 2020; we request 2023
//            var siteMetaJson = BuildSiteMetaJson(startDate: "01/01/2018", endDate: "31/12/2020");
//            SetupHttpFactory(siteMetaJson);

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo>());

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("0", result);
//        }

//        [Fact]
//        public async Task GetAtomDataSelectionStation_IncludesSites_WhenPollutantHasNoEndDate()
//        {
//            // No end date → still active; should be included for any year after start
//            var siteMetaJson = JsonSerializer.Serialize(new
//            {
//                member = new[]
//                {
//                    new
//                    {
//                        siteName = "Active Site",
//                        localSiteId = "SITE002",
//                        areaType = "Urban",
//                        siteType = "Background",
//                        governmentRegion = "London",
//                        latitude = "51.5",
//                        longitude = "-0.1",
//                        pollutantsMetaData = new Dictionary<string, object>
//                        {
//                            ["NO2"] = new { name = "Nitrogen dioxide", startDate = "01/01/2015", endDate = "" }
//                        }
//                    }
//                }
//            });

//            SetupHttpFactory(siteMetaJson);

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE002" } });

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("1", result);
//        }

//        // ------------------------------------------------------------------
//        // Deduplication: duplicate LocalSiteId entries are reduced to one
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_DeduplicatesBySiteId()
//        {
//            var siteMetaJson = JsonSerializer.Serialize(new
//            {
//                member = new[]
//                {
//                    new
//                    {
//                        siteName = "Site A",
//                        localSiteId = "DUPE",
//                        areaType = "Urban",
//                        siteType = "Background",
//                        governmentRegion = "London",
//                        latitude = "51.5",
//                        longitude = "-0.1",
//                        pollutantsMetaData = new Dictionary<string, object>
//                        {
//                            ["NO2"] = new { name = "Nitrogen dioxide", startDate = "01/01/2023", endDate = "31/12/2023" }
//                        }
//                    },
//                    new
//                    {
//                        siteName = "Site B",
//                        localSiteId = "DUPE",
//                        areaType = "Suburban",
//                        siteType = "Traffic",
//                        governmentRegion = "London",
//                        latitude = "51.6",
//                        longitude = "-0.2",
//                        pollutantsMetaData = new Dictionary<string, object>
//                        {
//                            ["NO2"] = new { name = "Nitrogen dioxide", startDate = "01/01/2023", endDate = "31/12/2023" }
//                        }
//                    }
//                }
//            });

//            SetupHttpFactory(siteMetaJson);

//            List<SiteInfo>? capturedSites = null;
//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
//                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
//                .ReturnsAsync(new List<SiteInfo>());

//            await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            // Only one site should reach the boundary service after deduplication
//            Assert.NotNull(capturedSites);
//            Assert.Single(capturedSites!);
//        }

//        // ------------------------------------------------------------------
//        // Multiple years in query string
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_HandlesMultipleYears_InYearParam()
//        {
//            SetupHttpFactory(BuildSiteMetaJson(startDate: "01/01/2022", endDate: "31/12/2022"));

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2021,2022,2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            // Year 2022 overlaps → site should be counted
//            Assert.Equal("1", result);
//        }

//        // ------------------------------------------------------------------
//        // ParseSiteMeta: sites without matching pollutants are excluded
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ExcludesSites_WithNoMatchingPollutants()
//        {
//            // Site only has SO2; we query for NO2
//            var siteMetaJson = BuildSiteMetaJson(pollutantName: "Sulphur dioxide");
//            SetupHttpFactory(siteMetaJson);

//            List<SiteInfo>? capturedSites = null;
//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
//                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
//                .ReturnsAsync(new List<SiteInfo>());

//            await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.NotNull(capturedSites);
//            Assert.Empty(capturedSites!);
//        }

//        // ------------------------------------------------------------------
//        // ParseSiteMeta: empty "member" array → no sites
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsZero_WhenMemberArrayIsEmpty()
//        {
//            SetupHttpFactory("{\"member\": []}");

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo>());

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("0", result);
//        }

//        // ------------------------------------------------------------------
//        // ParseSiteMeta: JSON without "member" property → no sites
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsZero_WhenMemberPropertyAbsent()
//        {
//            SetupHttpFactory("{\"data\": []}");

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo>());

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("0", result);
//        }

//        // ------------------------------------------------------------------
//        // ParseSiteMeta: site without "pollutantsMetaData" property → no pollutants
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_HandlesSite_WithNoPollutantsMetaData()
//        {
//            var siteMetaJson = JsonSerializer.Serialize(new
//            {
//                member = new[]
//                {
//                    new
//                    {
//                        siteName = "No Pollutant Site",
//                        localSiteId = "NOPOLL",
//                        areaType = "Urban",
//                        siteType = "Background",
//                        governmentRegion = "London",
//                        latitude = "51.5",
//                        longitude = "-0.1"
//                    }
//                }
//            });

//            SetupHttpFactory(siteMetaJson);

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo>());

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("0", result);
//        }

//        // ------------------------------------------------------------------
//        // Multiple pollutants requested (comma-separated)
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_HandlesMultiplePollutants_CommaSeparated()
//        {
//            SetupHttpFactory(BuildSiteMetaJson(pollutantName: "Sulphur dioxide"));

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

//            // Requesting NO2,SO2 → SO2 site should match
//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2,SO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("1", result);
//        }

//        // ------------------------------------------------------------------
//        // S3 service throws → presignedUrl path returns "Failure"
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenS3Throws()
//        {
//            SetupHttpFactory(BuildSiteMetaJson());

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

//            _hourlyFetchMock
//                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<FinalData>());

//            _s3Mock
//                .Setup(s => s.writecsvtoawss3bucket(It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), It.IsAny<string>()))
//                .ThrowsAsync(new Exception("S3 error"));

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorHourly", "dataSelectorMultiple", "u@t.com");

//            Assert.Equal("Failure", result);
//        }

//        // ------------------------------------------------------------------
//        // Boundary service throws → returns "Failure"
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenBoundaryServiceThrows()
//        {
//            SetupHttpFactory(BuildSiteMetaJson());

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ThrowsAsync(new Exception("boundary error"));

//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("Failure", result);
//        }

//        // ------------------------------------------------------------------
//        // Invalid year values (non-numeric) in year param are skipped
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_HandlesInvalidYearValues_Gracefully()
//        {
//            SetupHttpFactory(BuildSiteMetaJson(startDate: "01/01/2023", endDate: "31/12/2023"));

//            _boundaryServiceMock
//                .Setup(b => b.GetAtomDataSelectionStationBoundryService(It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync(new List<SiteInfo>());

//            // "abc" is invalid year — should be silently skipped
//            var result = await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "abc,2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            Assert.Equal("0", result);
//        }

//        // ------------------------------------------------------------------
//        // Logging: errors are logged when token retrieval fails
//        // ------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomDataSelectionStation_LogsError_WhenTokenIsNull()
//        {
//            _authServiceMock
//                .Setup(a => a.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync((string?)null);

//            var client = new HttpClient { BaseAddress = new Uri("https://api.test/") };
//            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);

//            await CreateService().GetAtomDataSelectionStation(
//                "NO2", "src", "2023", "England", "Country",
//                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

//            _loggerMock.Verify(
//                x => x.Log(
//                    LogLevel.Error,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, _) => true),
//                    It.IsAny<Exception>(),
//                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//                Times.AtLeastOnce);
//        }
//    }
//}