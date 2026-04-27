using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MongoDB.Driver;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    // =========================================================================
    // AuthService tests
    // =========================================================================
    public class AuthServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<HttpMessageHandler> _handlerMock = new();

        private AuthService CreateService()
        {
            var client = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri("https://api.test/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);
            return new AuthService(_httpClientFactoryMock.Object);
        }

        private void SetupResponse(HttpStatusCode status, string body)
        {
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsNull_WhenResponseNotSuccessful()
        {
            SetupResponse(HttpStatusCode.Unauthorized, "{}");
            var result = await CreateService().GetTokenAsync("user@test.com", "pass");
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsToken_FromTokenProperty()
        {
            SetupResponse(HttpStatusCode.OK, "{\"token\": \"abc123\"}");
            var result = await CreateService().GetTokenAsync("u", "p");
            Assert.Equal("abc123", result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsToken_FromAccessTokenProperty()
        {
            SetupResponse(HttpStatusCode.OK, "{\"access_token\": \"tok_access\"}");
            var result = await CreateService().GetTokenAsync("u", "p");
            Assert.Equal("tok_access", result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsToken_FromJwtProperty()
        {
            SetupResponse(HttpStatusCode.OK, "{\"jwt\": \"jwt_value\"}");
            var result = await CreateService().GetTokenAsync("u", "p");
            Assert.Equal("jwt_value", result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsToken_WhenRootIsString()
        {
            SetupResponse(HttpStatusCode.OK, "\"plain_token\"");
            var result = await CreateService().GetTokenAsync("u", "p");
            Assert.Equal("plain_token", result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsNull_WhenNoKnownTokenProperty()
        {
            SetupResponse(HttpStatusCode.OK, "{\"unknown_key\": \"value\"}");
            var result = await CreateService().GetTokenAsync("u", "p");
            Assert.Null(result);
        }
    }

    // =========================================================================
    // AtomDataSelectionStationService tests
    // =========================================================================
    public class AtomDataSelectionStationServiceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<IAtomDataSelectionStationBoundryService> _boundaryServiceMock = new();
        private readonly Mock<IAtomDataSelectionLocalAuthoritiesService> _localAuthMock = new();
        private readonly Mock<IAtomDataSelectionHourlyFetchService> _hourlyFetchMock = new();
        private readonly Mock<IAtomDataSelectionServices> _atomDataSelectionServicesMock = new();
        private readonly Mock<IAwss3BucketService> _s3Mock = new();
        private readonly Mock<IAuthService> _authServiceMock = new();
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock = new();
        private readonly Mock<HttpMessageHandler> _siteMetaHandlerMock = new();

        // ------------------------------------------------------------------
        // Service factory
        // ------------------------------------------------------------------

        private AtomDataSelectionStationService CreateService()
        {
            _atomDataSelectionServicesMock.Setup(a => a.StationBoundry).Returns(_boundaryServiceMock.Object);
            _atomDataSelectionServicesMock.Setup(a => a.LocalAuthorities).Returns(_localAuthMock.Object);
            _atomDataSelectionServicesMock.Setup(a => a.HourlyFetch).Returns(_hourlyFetchMock.Object);

            return new AtomDataSelectionStationService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _atomDataSelectionServicesMock.Object,
                _s3Mock.Object,
                _authServiceMock.Object,
                _mongoFactoryMock.Object);
        }

        // ------------------------------------------------------------------
        // MongoDB helpers
        // ------------------------------------------------------------------

        /// <summary>Creates a mock IMongoCollection that returns <paramref name="items"/> on FindAsync.</summary>
        private static Mock<IMongoCollection<T>> CreateCollectionMock<T>(IEnumerable<T>? items = null)
        {
            var list = (items ?? Enumerable.Empty<T>()).ToList();

            var cursorMock = new Mock<IAsyncCursor<T>>();
            cursorMock.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(list.Count > 0)
                      .ReturnsAsync(false);
            cursorMock.Setup(c => c.Current).Returns(list);

            var collMock = new Mock<IMongoCollection<T>>();
            collMock.Setup(c => c.FindAsync(
                        It.IsAny<FilterDefinition<T>>(),
                        It.IsAny<FindOptions<T, T>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(cursorMock.Object);

            return collMock;
        }

        /// <summary>
        /// Registers a PollutantMasterDocument collection mock.
        /// Must be called in every test because ResolvePollutantNameAsync always queries it.
        /// </summary>
        private void SetupPollutantMasterCollection(IEnumerable<PollutantMasterDocument>? docs = null)
        {
            var collMock = CreateCollectionMock(docs);
            _mongoFactoryMock
                .Setup(m => m.GetCollection<PollutantMasterDocument>(It.IsAny<string>()))
                .Returns(collMock.Object);
        }

        /// <summary>Registers a StationDetailDocument collection mock (NON-AURN path).</summary>
        private void SetupStationDetailCollection(IEnumerable<StationDetailDocument>? docs = null)
        {
            var collMock = CreateCollectionMock(docs);
            _mongoFactoryMock
                .Setup(m => m.GetCollection<StationDetailDocument>(It.IsAny<string>()))
                .Returns(collMock.Object);
        }

        /// <summary>
        /// Registers a JobDocument collection mock with all required operations
        /// (index creation, insert, and update).
        /// </summary>
        private void SetupJobCollection()
        {
            var indexManagerMock = new Mock<IMongoIndexManager<JobDocument>>();
            indexManagerMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<JobDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("index_name");

            var collMock = new Mock<IMongoCollection<JobDocument>>();
            collMock.Setup(c => c.Indexes).Returns(indexManagerMock.Object);
            collMock.Setup(c => c.InsertOneAsync(It.IsAny<JobDocument>(), null, default))
                    .Returns(Task.CompletedTask);
            collMock.Setup(c => c.UpdateOneAsync(
                        It.IsAny<FilterDefinition<JobDocument>>(),
                        It.IsAny<UpdateDefinition<JobDocument>>(),
                        It.IsAny<UpdateOptions>(),
                        default))
                    .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            _mongoFactoryMock
                .Setup(m => m.GetCollection<JobDocument>(It.IsAny<string>()))
                .Returns(collMock.Object);
        }

        // ------------------------------------------------------------------
        // HTTP / site-meta helpers
        // ------------------------------------------------------------------

        private static string BuildSiteMetaJson(
            string localSiteId = "SITE001",
            string siteName = "Test Site",
            string pollutantName = "Nitrogen dioxide",
            string startDate = "01/01/2023",
            string endDate = "31/12/2023")
        {
            return JsonSerializer.Serialize(new
            {
                member = new[]
                {
                    new
                    {
                        siteName,
                        localSiteId,
                        areaType = "Urban",
                        siteType = "Background",
                        governmentRegion = "London",
                        latitude = "51.5074",
                        longitude = "-0.1278",
                        pollutantsMetaData = new Dictionary<string, object>
                        {
                            ["NO2"] = new { name = pollutantName, startDate, endDate }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Sets up the HTTP mock (for the AURN site-meta call) and the auth service mock.
        /// For non-AURN tests the HTTP client is never called, so this is optional there.
        /// </summary>
        private void SetupHttpFactory(string siteMetaJson)
        {
            _authServiceMock
                .Setup(a => a.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("valid_token");

            _siteMetaHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(siteMetaJson, Encoding.UTF8, "application/json")
                });

            var client = new HttpClient(_siteMetaHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.test/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);
        }

        private void SetupBoundaryService(List<SiteInfo>? result = null)
        {
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(result ?? new List<SiteInfo>());
        }

        // ------------------------------------------------------------------
        // Null / empty input guards
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenPollutantNameIsNull()
        {
            var result = await CreateService().GetAtomDataSelectionStation(
                null, "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenPollutantNameIsEmpty()
        {
            var result = await CreateService().GetAtomDataSelectionStation(
                "", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenYearIsNull()
        {
            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", null, "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenYearIsEmpty()
        {
            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_LogsWarning_WhenPollutantNameIsNull()
        {
            await CreateService().GetAtomDataSelectionStation(
                null, "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ------------------------------------------------------------------
        // dataSelectorCount + datasource == "AURN"
        // (env vars absent → GetRicardoToken returns "Failure" string and logs error;
        //  FetchSiteMetadata is still called with the "Failure" bearer token)
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsCount_WhenAurnDataselectorCount()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            SetupHttpFactory(BuildSiteMetaJson());
            SetupBoundaryService(new List<SiteInfo>
            {
                new SiteInfo { LocalSiteId = "SITE001", Latitude = "51.5", Longitude = "-0.1" }
            });

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsZeroCount_WhenNoBoundaryMatchAurn()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            SetupHttpFactory(BuildSiteMetaJson());
            SetupBoundaryService(new List<SiteInfo>());

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("0", result);
        }

        // ------------------------------------------------------------------
        // GetRicardoToken branches (requires controlling RICARDO_API_* env vars)
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_LogsError_WhenRicardoEnvVarsMissing()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            SetupHttpFactory(BuildSiteMetaJson());
            SetupBoundaryService();

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_LogsError_WhenRicardoTokenReturnsNull()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", "test@test.com");
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", "pass");
            try
            {
                SetupPollutantMasterCollection();
                SetupHttpFactory(BuildSiteMetaJson());
                _authServiceMock
                    .Setup(a => a.GetTokenAsync("test@test.com", "pass"))
                    .ReturnsAsync((string?)null);
                SetupBoundaryService();

                await CreateService().GetAtomDataSelectionStation(
                    "NO2", "AURN", "2023", "England", "Country",
                    "dataSelectorCount", "dataSelectorSingle", "u@t.com");

                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Error,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, _) => true),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce);
            }
            finally
            {
                Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
                Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);
            }
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsCount_WhenRicardoTokenIsValid()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", "test@test.com");
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", "pass");
            try
            {
                SetupPollutantMasterCollection();
                _authServiceMock
                    .Setup(a => a.GetTokenAsync("test@test.com", "pass"))
                    .ReturnsAsync("real_token");
                SetupHttpFactory(BuildSiteMetaJson());
                // Override after SetupHttpFactory so the "real_token" setup wins
                _authServiceMock
                    .Setup(a => a.GetTokenAsync("test@test.com", "pass"))
                    .ReturnsAsync("real_token");
                SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

                var result = await CreateService().GetAtomDataSelectionStation(
                    "NO2", "AURN", "2023", "England", "Country",
                    "dataSelectorCount", "dataSelectorSingle", "u@t.com");

                Assert.Equal("1", result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
                Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);
            }
        }

        // ------------------------------------------------------------------
        // HTTP 500 on site-meta call → EnsureSuccessStatusCode throws → "Failure"
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenSiteMetaHttpFails()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", "test@test.com");
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", "pass");
            try
            {
                SetupPollutantMasterCollection();
                _authServiceMock
                    .Setup(a => a.GetTokenAsync("test@test.com", "pass"))
                    .ReturnsAsync("real_token");

                _siteMetaHandlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });

                var client = new HttpClient(_siteMetaHandlerMock.Object)
                {
                    BaseAddress = new Uri("https://api.test/")
                };
                _httpClientFactoryMock.Setup(f => f.CreateClient("RicardoNewAPI")).Returns(client);

                var result = await CreateService().GetAtomDataSelectionStation(
                    "NO2", "AURN", "2023", "England", "Country",
                    "dataSelectorCount", "dataSelectorSingle", "u@t.com");

                Assert.Equal("Failure", result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
                Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);
            }
        }

        // ------------------------------------------------------------------
        // dataSelectorCount + datasource == "NON-AURN"
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsNetworkTypeCounts_WhenNonAurnDataselectorCount()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection();
            SetupBoundaryService(new List<SiteInfo>
            {
                new SiteInfo { LocalSiteId = "S1", NetworkType = "AURN" },
                new SiteInfo { LocalSiteId = "S2", NetworkType = "AURN" },
                new SiteInfo { LocalSiteId = "S3", NetworkType = "LAQN" }
            });

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            // Should return a list of { NetworkType, Count } objects
            var items = Assert.IsAssignableFrom<IEnumerable>(result).Cast<object>().ToList();
            Assert.Equal(2, items.Count); // AURN group + LAQN group
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFallbackUnknown_WhenNonAurnCountEmptyStations()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection();
            SetupBoundaryService(new List<SiteInfo>());

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            // stationData is empty → networkTypeCounts.Count == 0 → fallback Unknown
            var items = Assert.IsAssignableFrom<IEnumerable>(result).Cast<object>().ToList();
            Assert.Single(items);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsNetworkTypeCounts_WhenNonAurnHasNullNetworkType()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection();
            SetupBoundaryService(new List<SiteInfo>
            {
                new SiteInfo { LocalSiteId = "S1", NetworkType = null }
            });

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            // null NetworkType → grouped under "Unknown"
            var items = Assert.IsAssignableFrom<IEnumerable>(result).Cast<object>().ToList();
            Assert.Single(items);
        }

        // ------------------------------------------------------------------
        // dataSelectorHourly + dataSelectorMultiple (presigned URL)
        // (datasource irrelevant for the dataSelectorHourly branch)
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsPresignedUrl_WhenDataselectorMultiple()
        {
            SetupPollutantMasterCollection();
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            _hourlyFetchMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData>());

            _s3Mock
                .Setup(s => s.WriteCsvToAwsS3BucketAsync(
                    It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), It.IsAny<string>()))
                .ReturnsAsync("https://s3.example.com/file.zip");

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "src", "2023", "England", "Country",
                "dataSelectorHourly", "dataSelectorMultiple", "u@t.com");

            Assert.Equal("https://s3.example.com/file.zip", result);
        }

        // ------------------------------------------------------------------
        // dataSelectorHourly + dataSelectorSingle (job queue)
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsJobId_WhenDataselectorSingle()
        {
            SetupPollutantMasterCollection();
            SetupJobCollection();
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "src", "2023", "England", "Country",
                "dataSelectorHourly", "dataSelectorSingle", "u@t.com");

            Assert.NotNull(result);
            Assert.Matches("^[a-f0-9]{32}$", result.ToString()!);
        }

        // ------------------------------------------------------------------
        // NON-AURN + dataSelectorHourly
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsPresignedUrl_WhenNonAurnDataselectorMultiple()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection();
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "S1" } });

            _hourlyFetchMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData>());

            _s3Mock
                .Setup(s => s.WriteCsvToAwsS3BucketAsync(
                    It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), It.IsAny<string>()))
                .ReturnsAsync("https://s3.example.com/nonaurn.zip");

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorHourly", "dataSelectorMultiple", "u@t.com");

            Assert.Equal("https://s3.example.com/nonaurn.zip", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsJobId_WhenNonAurnDataselectorSingle()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection();
            SetupJobCollection();
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "S1" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorHourly", "dataSelectorSingle", "u@t.com");

            Assert.NotNull(result);
            Assert.Matches("^[a-f0-9]{32}$", result.ToString()!);
        }

        // ------------------------------------------------------------------
        // Unknown dataselectorfiltertype → "Failure"
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenUnknownFilterType()
        {
            SetupPollutantMasterCollection();
            SetupBoundaryService();

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "src", "2023", "England", "Country",
                "unknownFilterType", "dataSelectorSingle", "u@t.com");

            Assert.Equal("Failure", result);
        }

        // ------------------------------------------------------------------
        // Exception thrown anywhere → "Failure"
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenExceptionThrown()
        {
            // Make ResolvePollutantNameAsync throw immediately
            _mongoFactoryMock
                .Setup(m => m.GetCollection<PollutantMasterDocument>(It.IsAny<string>()))
                .Throws(new Exception("unexpected"));

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "src", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenBoundaryServiceThrows()
        {
            SetupPollutantMasterCollection();
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("boundary error"));

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "src", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenS3Throws()
        {
            SetupPollutantMasterCollection();
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            _hourlyFetchMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData>());

            _s3Mock
                .Setup(s => s.WriteCsvToAwsS3BucketAsync(
                    It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("S3 error"));

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "src", "2023", "England", "Country",
                "dataSelectorHourly", "dataSelectorMultiple", "u@t.com");

            Assert.Equal("Failure", result);
        }

        // ------------------------------------------------------------------
        // Logging: error logged on exception
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_LogsError_WhenExceptionThrown()
        {
            _mongoFactoryMock
                .Setup(m => m.GetCollection<PollutantMasterDocument>(It.IsAny<string>()))
                .Throws(new Exception("unexpected"));

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "src", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ------------------------------------------------------------------
        // ResolvePollutantNameAsync: mongo returns actual documents
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ResolvesIds_FromMongoPollutantCollection()
        {
            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "44", pollutantName = "NO2" }
            });
            SetupStationDetailCollection();
            SetupBoundaryService();

            // "44" resolves to "NO2"; GetMappedPollutants("NO2") → ["Nitrogen dioxide"]
            // No station detail docs → filteredSites empty → NON-AURN count fallback
            var result = await CreateService().GetAtomDataSelectionStation(
                "44", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            var items = Assert.IsAssignableFrom<IEnumerable>(result).Cast<object>().ToList();
            Assert.Single(items); // fallback unknown
        }

        // ------------------------------------------------------------------
        // Pollutant mapping: known keys map to full names (AURN path)
        // Mongo maps pollutantInput → pollutantInput so GetMappedPollutants
        // can look it up by the short key.
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("NO2", "Nitrogen dioxide")]
        [InlineData("SO2", "Sulphur dioxide")]
        [InlineData("CO", "Carbon monoxide")]
        [InlineData("NOx", "Nitrogen oxides as nitrogen dioxide")]
        [InlineData("NO", "Nitric oxide")]
        [InlineData("Ozone", "Ozone")]
        public async Task GetAtomDataSelectionStation_MapsKnownPollutants_Correctly(
            string pollutantInput, string expectedMappedName)
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            // Mongo maps the short key back to itself so GetMappedPollutants can use it as a key
            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = pollutantInput, pollutantName = pollutantInput }
            });
            SetupHttpFactory(BuildSiteMetaJson(pollutantName: expectedMappedName));
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                pollutantInput, "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_IncludesUnknownPollutant_AsRawName()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument
                {
                    pollutantID = "SomeFuturePollutant",
                    pollutantName = "SomeFuturePollutant"
                }
            });
            SetupHttpFactory(BuildSiteMetaJson(pollutantName: "SomeFuturePollutant"));
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                "SomeFuturePollutant", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result);
        }

        // ------------------------------------------------------------------
        // "Fine particulate matter" → PM2.5 variants
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("PM<sub>2.5</sub> (Hourly measured)")]
        [InlineData("Volatile PM<sub>2.5</sub> (Hourly measured)")]
        [InlineData("Non-volatile PM<sub>2.5</sub> (Hourly measured)")]
        [InlineData("PM<sub>2.5</sub> particulate matter (Hourly measured)")]
        public async Task GetAtomDataSelectionStation_ReturnsCount_ForAllPm25MappedNames(string mappedName)
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            // Mongo maps "PM2.5" → "Fine particulate matter" (the pollutantMap key)
            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "PM2.5", pollutantName = "Fine particulate matter" }
            });
            SetupHttpFactory(BuildSiteMetaJson(pollutantName: mappedName));
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                "PM2.5", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result);
        }

        // ------------------------------------------------------------------
        // "Particulate matter" → PM10 variants
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("PM<sub>10</sub> (Hourly measured)")]
        [InlineData("Volatile PM<sub>10</sub> (Hourly measured)")]
        [InlineData("Non-volatile PM<sub>10</sub> (Hourly measured)")]
        [InlineData("PM<sub>10</sub> particulate matter (Hourly measured)")]
        public async Task GetAtomDataSelectionStation_ReturnsCount_ForAllPm10MappedNames(string mappedName)
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "PM10", pollutantName = "Particulate matter" }
            });
            SetupHttpFactory(BuildSiteMetaJson(pollutantName: mappedName));
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                "PM10", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result);
        }

        // ------------------------------------------------------------------
        // Year filtering
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_FiltersOutSites_WhenPollutantYearDoesNotOverlap()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            // Mongo maps "NO2" → "NO2"; GetMappedPollutants("NO2") → ["Nitrogen dioxide"]
            // Pollutant ended in 2020; request year 2023 → filtered out by year
            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "NO2", pollutantName = "NO2" }
            });
            SetupHttpFactory(BuildSiteMetaJson(startDate: "01/01/2018", endDate: "31/12/2020"));

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("0", result);
            Assert.NotNull(capturedSites);
            Assert.Empty(capturedSites!);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_IncludesSites_WhenPollutantHasNoEndDate()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            var siteMetaJson = JsonSerializer.Serialize(new
            {
                member = new[]
                {
                    new
                    {
                        siteName = "Active Site", localSiteId = "SITE002",
                        areaType = "Urban", siteType = "Background",
                        governmentRegion = "London", latitude = "51.5", longitude = "-0.1",
                        pollutantsMetaData = new Dictionary<string, object>
                        {
                            ["NO2"] = new { name = "Nitrogen dioxide", startDate = "01/01/2015", endDate = "" }
                        }
                    }
                }
            });
            SetupHttpFactory(siteMetaJson);
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE002" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_FiltersOutSites_WhenStartDateIsInvalid()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            // Invalid startDate format → IsPollutantInYearRange returns false → site excluded
            SetupHttpFactory(BuildSiteMetaJson(startDate: "not-a-date", endDate: "31/12/2023"));

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.NotNull(capturedSites);
            Assert.Empty(capturedSites!);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_HandlesMultipleYears_InYearParam()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            SetupHttpFactory(BuildSiteMetaJson(startDate: "01/01/2022", endDate: "31/12/2022"));
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2021,2022,2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result); // year 2022 overlaps
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_HandlesInvalidYearValues_Gracefully()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "NO2", pollutantName = "NO2" }
            });
            SetupHttpFactory(BuildSiteMetaJson(startDate: "01/01/2023", endDate: "31/12/2023"));

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            // "abc" is not a valid year → only "2023" is parsed; site IS in range
            await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "abc,2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            // Site has valid 2023 dates and "abc" is skipped; site reaches boundary service
            Assert.NotNull(capturedSites);
            Assert.Single(capturedSites!);
        }

        // ------------------------------------------------------------------
        // Deduplication: duplicate LocalSiteId entries are reduced to one
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_DeduplicatesBySiteId()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            var siteMetaJson = JsonSerializer.Serialize(new
            {
                member = new[]
                {
                    new
                    {
                        siteName = "Site A", localSiteId = "DUPE", areaType = "Urban",
                        siteType = "Background", governmentRegion = "London",
                        latitude = "51.5", longitude = "-0.1",
                        pollutantsMetaData = new Dictionary<string, object>
                        {
                            ["NO2"] = new
                            {
                                name = "Nitrogen dioxide",
                                startDate = "01/01/2023", endDate = "31/12/2023"
                            }
                        }
                    },
                    new
                    {
                        siteName = "Site B", localSiteId = "DUPE", areaType = "Suburban",
                        siteType = "Traffic", governmentRegion = "London",
                        latitude = "51.6", longitude = "-0.2",
                        pollutantsMetaData = new Dictionary<string, object>
                        {
                            ["NO2"] = new
                            {
                                name = "Nitrogen dioxide",
                                startDate = "01/01/2023", endDate = "31/12/2023"
                            }
                        }
                    }
                }
            });
            SetupHttpFactory(siteMetaJson);

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.NotNull(capturedSites);
            Assert.Single(capturedSites!);
        }

        // ------------------------------------------------------------------
        // ParseSiteMeta: pollutant mismatch → site excluded
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ExcludesSites_WithNoMatchingPollutants()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            // Mongo maps "NO2" → "NO2"; GetMappedPollutants("NO2") → ["Nitrogen dioxide"]
            // Site has "Sulphur dioxide" which is not "Nitrogen dioxide" → excluded
            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "NO2", pollutantName = "NO2" }
            });
            SetupHttpFactory(BuildSiteMetaJson(pollutantName: "Sulphur dioxide"));

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.NotNull(capturedSites);
            Assert.Empty(capturedSites!);
        }

        // ------------------------------------------------------------------
        // ParseSiteMeta edge cases
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsZero_WhenMemberArrayIsEmpty()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            SetupHttpFactory("{\"member\": []}");
            SetupBoundaryService();

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("0", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsZero_WhenMemberPropertyAbsent()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection();
            SetupHttpFactory("{\"data\": []}");
            SetupBoundaryService();

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("0", result);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_HandlesSite_WithNoPollutantsMetaData()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "NO2", pollutantName = "NO2" }
            });
            var siteMetaJson = JsonSerializer.Serialize(new
            {
                member = new[]
                {
                    new
                    {
                        siteName = "No Pollutant Site", localSiteId = "NOPOLL",
                        areaType = "Urban", siteType = "Background",
                        governmentRegion = "London", latitude = "51.5", longitude = "-0.1"
                        // no pollutantsMetaData property
                    }
                }
            });
            SetupHttpFactory(siteMetaJson);
            SetupBoundaryService();

            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("0", result);
        }

        // ------------------------------------------------------------------
        // Multiple comma-separated pollutants
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetAtomDataSelectionStation_HandlesMultiplePollutants_CommaSeparated()
        {
            Environment.SetEnvironmentVariable("RICARDO_API_KEY", null);
            Environment.SetEnvironmentVariable("RICARDO_API_VALUE", null);

            // Mongo maps both IDs → keys; SO2 maps to "Sulphur dioxide"
            SetupPollutantMasterCollection(new[]
            {
                new PollutantMasterDocument { pollutantID = "NO2", pollutantName = "NO2" },
                new PollutantMasterDocument { pollutantID = "SO2", pollutantName = "SO2" }
            });
            SetupHttpFactory(BuildSiteMetaJson(pollutantName: "Sulphur dioxide"));
            SetupBoundaryService(new List<SiteInfo> { new SiteInfo { LocalSiteId = "SITE001" } });

            // Requesting NO2,SO2 → SO2 site should match
            var result = await CreateService().GetAtomDataSelectionStation(
                "NO2,SO2", "AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            Assert.Equal("1", result);
        }

        // ------------------------------------------------------------------
        // SplitEnvironmentType: covered via NON-AURN → GetSiteInfoAsync path
        // ------------------------------------------------------------------

        [Fact]
        public async Task GetSiteInfoAsync_Maps_NullEnvironmentType_ToBothNull()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection(new[]
            {
                new StationDetailDocument
                {
                    SiteID = "S1", SiteName = "Site1", EnvironmentType = null,
                    Latitude = "51.5", Longitude = "-0.1", NetworkType = "LAQN",
                    pollutantID = "NO2", PollutantName = "Nitrogen dioxide",
                    StartDate = "01/01/2023", EndDate = "31/12/2023"
                }
            });

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            var site = capturedSites?.FirstOrDefault(s => s.LocalSiteId == "S1");
            Assert.NotNull(site);
            Assert.Null(site!.AreaType);
            Assert.Null(site.SiteType);
        }

        [Fact]
        public async Task GetSiteInfoAsync_Maps_SingleWordEnvironmentType_ToSiteTypeOnly()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection(new[]
            {
                new StationDetailDocument
                {
                    SiteID = "S2", SiteName = "Site2", EnvironmentType = "Background",
                    Latitude = "51.5", Longitude = "-0.1", NetworkType = "LAQN",
                    pollutantID = "NO2", PollutantName = "Nitrogen dioxide",
                    StartDate = "01/01/2023", EndDate = "31/12/2023"
                }
            });

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            var site = capturedSites?.FirstOrDefault(s => s.LocalSiteId == "S2");
            Assert.NotNull(site);
            Assert.Null(site!.AreaType);
            Assert.Equal("Background", site.SiteType);
        }

        [Fact]
        public async Task GetSiteInfoAsync_Maps_MultiWordEnvironmentType_ToAreaTypeAndSiteType()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection(new[]
            {
                new StationDetailDocument
                {
                    SiteID = "S3", SiteName = "Site3", EnvironmentType = "Urban Background",
                    Latitude = "51.5", Longitude = "-0.1", NetworkType = "LAQN",
                    pollutantID = "NO2", PollutantName = "Nitrogen dioxide",
                    StartDate = "01/01/2023", EndDate = "31/12/2023"
                }
            });

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, string, string>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            var site = capturedSites?.FirstOrDefault(s => s.LocalSiteId == "S3");
            Assert.NotNull(site);
            Assert.Equal("Urban", site!.AreaType);
            Assert.Equal("Background", site.SiteType);
        }

        [Fact]
        public async Task GetSiteInfoAsync_Maps_WhitespaceEnvironmentType_ToBothNull()
        {
            SetupPollutantMasterCollection();
            SetupStationDetailCollection(new[]
            {
                new StationDetailDocument
                {
                    SiteID = "S4", EnvironmentType = "   ",
                    pollutantID = "NO2", PollutantName = "Nitrogen dioxide",
                    StartDate = "01/01/2023", EndDate = "31/12/2023"
                }
            });

            List<SiteInfo>? capturedSites = null;
            _boundaryServiceMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<List<SiteInfo>, String, String>((s, _, __) => capturedSites = s)
                .ReturnsAsync(new List<SiteInfo>());

            await CreateService().GetAtomDataSelectionStation(
                "NO2", "NON-AURN", "2023", "England", "Country",
                "dataSelectorCount", "dataSelectorSingle", "u@t.com");

            var site = capturedSites?.FirstOrDefault(s => s.LocalSiteId == "S4");
            Assert.NotNull(site);
            Assert.Null(site!.AreaType);
            Assert.Null(site.SiteType);
        }
    }
}