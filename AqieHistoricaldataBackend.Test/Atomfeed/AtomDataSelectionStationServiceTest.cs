using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using Amazon.S3;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionStationServiceTest
    {
        private const string Failure = "Failure";

        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<IAtomDataSelectionServices> _servicesMock = new();
        private readonly Mock<IAtomDataSelectionStationBoundryService> _boundryMock = new();
        private readonly Mock<IAtomDataSelectionHourlyFetchService> _hourlyMock = new();
        private readonly Mock<IAwss3BucketService> _s3Mock = new();
        private readonly Mock<IAuthService> _authMock = new();
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock = new();

        private readonly Mock<IMongoCollection<PollutantMasterDocument>> _pollutantCollectionMock = new();
        private readonly Mock<IMongoCollection<StationDetailDocument>> _stationCollectionMock = new();
        private readonly Mock<IMongoCollection<JobDocument>> _jobCollectionMock = new();
        private readonly Mock<IMongoIndexManager<JobDocument>> _indexManagerMock = new();

        private readonly AtomDataSelectionStationService _sut;

        public AtomDataSelectionStationServiceTest()
        {
            _servicesMock.Setup(s => s.StationBoundry).Returns(_boundryMock.Object);
            _servicesMock.Setup(s => s.HourlyFetch).Returns(_hourlyMock.Object);

            _jobCollectionMock.Setup(c => c.Indexes).Returns(_indexManagerMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<PollutantMasterDocument>("aqie_atom_non_aurn_networks_pollutant_master"))
                .Returns(_pollutantCollectionMock.Object);
            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details"))
                .Returns(_stationCollectionMock.Object);
            _mongoFactoryMock
                .Setup(f => f.GetCollection<JobDocument>("aqie_csvexport_jobs"))
                .Returns(_jobCollectionMock.Object);

            SetupPollutantMaster(new PollutantMasterDocument { pollutantID = "1", pollutantName = "NO2" });
            SetupStationDetails();
            SetupHttpClient(RicardoMetadataJson);

            _authMock.Setup(a => a.GetRicardoToken()).ReturnsAsync("token");

            _sut = new AtomDataSelectionStationService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _servicesMock.Object,
                _s3Mock.Object,
                _authMock.Object,
                _mongoFactoryMock.Object);
        }

        // ───────────── Helpers ─────────────

        private sealed class StubHandler(string content, HttpStatusCode status) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                });
        }

        private void SetupHttpClient(string content, HttpStatusCode status = HttpStatusCode.OK)
        {
            var client = new HttpClient(new StubHandler(content, status))
            {
                BaseAddress = new Uri("https://ricardo.example.com/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        }

        private static Mock<IAsyncCursor<T>> BuildCursor<T>(List<T> items)
        {
            var cursor = new Mock<IAsyncCursor<T>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(items.Count > 0)
                  .ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(items);
            return cursor;
        }

        private void SetupPollutantMaster(params PollutantMasterDocument[] docs)
        {
            var cursor = BuildCursor(docs.ToList());
            _pollutantCollectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<PollutantMasterDocument>>(),
                    It.IsAny<FindOptions<PollutantMasterDocument, PollutantMasterDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursor.Object);
        }

        private void SetupStationDetails(params StationDetailDocument[] docs)
        {
            var cursor = BuildCursor(docs.ToList());
            _stationCollectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<StationDetailDocument>>(),
                    It.IsAny<FindOptions<StationDetailDocument, StationDetailDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursor.Object);
        }

        private void SetupBoundry(List<SiteInfo> result)
        {
            _boundryMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(result);
        }

        private static SiteInfo Site(string id, string? networkType = null) => new()
        {
            LocalSiteId = id,
            SiteName = "Site " + id,
            RegionId = "1",
            NetworkType = networkType,
            Pollutants = new List<PollutantInfo>
            {
                new() { Name = "Nitrogen dioxide", StartDate = "01/01/2020", EndDate = "31/12/2025" }
            }
        };

        private static QueryStringData Query(
            string? pollutant = "1",
            string? year = "2024",
            string? source = "AURN",
            string? filterType = "dataSelectorCount",
            string? downloadType = null,
            string? regionId = null,
            string? networkId = null) => new()
            {
                pollutantName = pollutant,
                Year = year,
                dataSource = source,
                dataselectorfiltertype = filterType,
                dataselectordownloadtype = downloadType,
                RegionId = regionId,
                networkId = networkId,
                Region = "London",
                regiontype = "governmentRegion",
                email = "test@example.com"
            };

        // ───────────── Guard clauses ─────────────

        [Theory]
        [InlineData(null, "2024")]
        [InlineData("", "2024")]
        [InlineData("1", null)]
        [InlineData("1", "")]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenPollutantOrYearMissing(string? pollutant, string? year)
        {
            var result = await _sut.GetAtomDataSelectionStation(Query(pollutant, year));

            Assert.Equal(Failure, result);
            _mongoFactoryMock.Verify(f => f.GetCollection<PollutantMasterDocument>(It.IsAny<string>()), Times.Never);
        }

        // ───────────── AURN count ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsStationCount_ForAurnCount()
        {
            SetupBoundry(new List<SiteInfo> { Site("A"), Site("B") });

            var result = await _sut.GetAtomDataSelectionStation(Query());

            Assert.Equal("2", result);
            _authMock.Verify(a => a.GetRicardoToken(), Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_AppliesRegionFilter_WhenRegionIdProvided()
        {
            SetupBoundry(new List<SiteInfo> { Site("A") });

            var result = await _sut.GetAtomDataSelectionStation(Query(regionId: "1"));

            Assert.Equal("1", result);
        }

        // ───────────── NON-AURN count ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsNetworkTypeCounts_ForNonAurnCount()
        {
            SetupStationDetails(new StationDetailDocument
            {
                SiteID = "S1",
                SiteName = "Station 1",
                NetworkID = "N1",
                NetworkType = "Industrial",
                pollutantID = "1",
                PollutantName = "Nitrogen dioxide",
                EnvironmentType = "Urban Background",
                StartDate = "01/01/2020",
                EndDate = "31/12/2025"
            });
            SetupBoundry(new List<SiteInfo> { Site("A", "Industrial"), Site("B", "Industrial"), Site("C", "Rural") });

            var result = await _sut.GetAtomDataSelectionStation(
                Query(source: "NON-AURN", networkId: "N1"));

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.Contains("Industrial", json);
            Assert.Contains("Rural", json);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsUnknownBucket_WhenNonAurnStationDataEmpty()
        {
            SetupBoundry(new List<SiteInfo>());

            var result = await _sut.GetAtomDataSelectionStation(Query(source: "NON-AURN"));

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.Contains("Unknown", json);
            Assert.Contains("0", json);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_GroupsNullNetworkTypeAsUnknown()
        {
            SetupBoundry(new List<SiteInfo> { Site("A"), Site("B") });

            var result = await _sut.GetAtomDataSelectionStation(Query(source: "NON-AURN"));

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.Contains("Unknown", json);
        }

        // ───────────── Unsupported filter type ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_ForUnknownFilterType()
        {
            SetupBoundry(new List<SiteInfo> { Site("A") });

            var result = await _sut.GetAtomDataSelectionStation(Query(filterType: "somethingElse"));

            Assert.Equal(Failure, result);
        }

        // ───────────── Hourly — email (multiple) download ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsPresignedUrl_ForEmailDownload()
        {
            SetupBoundry(new List<SiteInfo> { Site("A") });
            _hourlyMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData> { new() });
            _s3Mock
                .Setup(s => s.WriteCsvToAwsS3BucketAsync(
                    It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), "dataSelectorMultiple"))
                .ReturnsAsync("https://s3/presigned.csv");

            var result = await _sut.GetAtomDataSelectionStation(
                Query(filterType: "dataSelectorHourly", downloadType: "dataSelectorMultiple"));

            Assert.Equal("https://s3/presigned.csv", result);
            _s3Mock.VerifyAll();
        }

        // ───────────── Hourly — single (queued job) ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsJobId_AndProcessesJobSuccessfully()
        {
            SetupBoundry(new List<SiteInfo> { Site("A") });

            JobDocument? inserted = null;
            _jobCollectionMock
                .Setup(c => c.InsertOneAsync(It.IsAny<JobDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<JobDocument, InsertOneOptions, CancellationToken>((d, _, _) => inserted = d)
                .Returns(Task.CompletedTask);

            var completed = new TaskCompletionSource();
            var updates = new List<UpdateDefinition<JobDocument>>();
            _jobCollectionMock
                .Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<JobDocument>>(),
                    It.IsAny<UpdateDefinition<JobDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<JobDocument>, UpdateDefinition<JobDocument>, UpdateOptions, CancellationToken>(
                    (_, u, _, _) =>
                    {
                        updates.Add(u);
                        if (updates.Count == 2) completed.TrySetResult();
                    })
                .ReturnsAsync(Mock.Of<UpdateResult>());

            _hourlyMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData> { new() });
            _s3Mock
                .Setup(s => s.WriteCsvToAwsS3BucketAsync(
                    It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), It.IsAny<string>()))
                .ReturnsAsync("https://s3/job-result.csv");

            var result = await _sut.GetAtomDataSelectionStation(
                Query(filterType: "dataSelectorHourly", downloadType: "dataSelectorSingle"));

            var jobId = Assert.IsType<string>(result);
            Assert.False(string.IsNullOrWhiteSpace(jobId));
            Assert.NotNull(inserted);
            Assert.Equal(jobId, inserted!.JobId);
            Assert.Equal(JobStatusEnum.Pending, inserted.Status);

            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

            _indexManagerMock.Verify(i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<JobDocument>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            _s3Mock.Verify(s => s.WriteCsvToAwsS3BucketAsync(
                It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), "dataSelectorSingle"), Times.Once);
            Assert.Equal(2, updates.Count); // Processing + Completed
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_MarksJobFailed_WhenBackgroundProcessingThrows()
        {
            SetupBoundry(new List<SiteInfo> { Site("A") });

            _jobCollectionMock
                .Setup(c => c.InsertOneAsync(It.IsAny<JobDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var failedUpdate = new TaskCompletionSource();
            var updateCount = 0;
            _jobCollectionMock
                .Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<JobDocument>>(),
                    It.IsAny<UpdateDefinition<JobDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    if (Interlocked.Increment(ref updateCount) == 2) failedUpdate.TrySetResult();
                })
                .ReturnsAsync(Mock.Of<UpdateResult>());

            _hourlyMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryStringData>()))
                .ThrowsAsync(new InvalidOperationException("hourly fetch failed"));

            var result = await _sut.GetAtomDataSelectionStation(
                Query(filterType: "dataSelectorHourly", downloadType: "dataSelectorSingle"));

            Assert.False(string.IsNullOrWhiteSpace(result as string));

            await failedUpdate.Task.WaitAsync(TimeSpan.FromSeconds(10));

            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.AtLeastOnce);
        }

        // ───────────── Exception handling in the outer try/catch ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenBoundryServiceThrows()
        {
            _boundryMock
                .Setup(b => b.GetAtomDataSelectionStationBoundryService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("boundary failure"));

            var result = await _sut.GetAtomDataSelectionStation(Query());

            Assert.Equal(Failure, result);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenTokenServiceThrows()
        {
            _authMock.Setup(a => a.GetRicardoToken()).ThrowsAsync(new HttpRequestException("token failure"));

            var result = await _sut.GetAtomDataSelectionStation(Query());

            Assert.Equal(Failure, result);
        }

        private const string RicardoMetadataJson = """
        {
          "member": [
            {
              "siteName": "Site A",
              "localSiteId": "A",
              "areaType": "Urban",
              "siteType": "Background",
              "governmentRegion": "London",
              "zoneRegion": "1",
              "latitude": "51.5",
              "longitude": "-0.1",
              "pollutantsMetaData": {
                "no2": {
                  "pollutantName": "Nitrogen dioxide",
                  "startDate": "01/01/2020",
                  "endDate": "31/12/2025"
                }
              }
            }
          ]
        }
        """;

        // ───────────── AURN → NON-AURN re-mapping for non-AURN pollutants ─────────────

        [Theory]
        [InlineData("10")]
        [InlineData("43")]
        [InlineData("179")]
        public async Task GetAtomDataSelectionStation_SwitchesToNonAurn_WhenPollutantIsNonAurn(string pollutantId)
        {
            SetupPollutantMaster(new PollutantMasterDocument { pollutantID = pollutantId, pollutantName = "Benzene" });
            SetupStationDetails(new StationDetailDocument
            {
                SiteID = "S1",
                SiteName = "Station 1",
                NetworkID = "10",
                NetworkType = "Industrial",
                pollutantID = pollutantId,
                PollutantName = "Benzene",
                EnvironmentType = "Urban Background",
                StartDate = "01/01/2020",
                EndDate = "31/12/2025"
            });
            SetupBoundry(new List<SiteInfo> { Site("A", "Industrial"), Site("B", "Industrial") });

            // datasource "AURN" + non-AURN pollutant => NON-AURN + networkId 10 => flipped back to AURN
            var result = await _sut.GetAtomDataSelectionStation(Query(pollutant: pollutantId));

            Assert.Equal("2", result);                      // AURN count branch taken
            _authMock.Verify(a => a.GetRicardoToken(), Times.Never); // Ricardo metadata not used
            _mongoFactoryMock.Verify(
                f => f.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details"),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_KeepsNonAurn_WhenNetworkIdIsNotTen()
        {
            SetupBoundry(new List<SiteInfo> { Site("A", "Industrial") });

            var result = await _sut.GetAtomDataSelectionStation(Query(source: "NON-AURN", networkId: "7"));

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.Contains("Industrial", json); // NON-AURN grouped-count branch
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_UsesEmptyNetworkId_WhenNetworkIdIsNull()
        {
            SetupBoundry(new List<SiteInfo> { Site("A", "Rural") });

            var result = await _sut.GetAtomDataSelectionStation(Query(source: "NON-AURN", networkId: null));

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.Contains("Rural", json);
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_SkipsRegionFilter_ForNonAurnWithRegionId()
        {
            SetupBoundry(new List<SiteInfo> { Site("A", "Industrial"), Site("B", "Industrial") });

            var result = await _sut.GetAtomDataSelectionStation(
                Query(source: "NON-AURN", networkId: "7", regionId: "999"));

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            Assert.Contains("Industrial", json); // region filter not applied for NON-AURN
        }

        // ───────────── Unknown data source (neither AURN nor NON-AURN) ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_ForUnknownDataSource()
        {
            SetupBoundry(new List<SiteInfo>());

            var result = await _sut.GetAtomDataSelectionStation(Query(source: "OTHER"));

            Assert.Equal(Failure, result);
            _authMock.Verify(a => a.GetRicardoToken(), Times.Never);
        }

        // ───────────── Hourly download after re-mapping ─────────────

        [Fact]
        public async Task GetAtomDataSelectionStation_UsesResolvedPollutantName_ForHourlyDownload()
        {
            SetupBoundry(new List<SiteInfo> { Site("A") });

            string? capturedPollutant = null;
            _hourlyMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryStringData>()))
                .Callback<List<SiteInfo>, string, string, QueryStringData>((_, p, _, _) => capturedPollutant = p)
                .ReturnsAsync(new List<FinalData> { new() });
            _s3Mock
                .Setup(s => s.WriteCsvToAwsS3BucketAsync(
                    It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), It.IsAny<string>()))
                .ReturnsAsync("https://s3/resolved.csv");

            var result = await _sut.GetAtomDataSelectionStation(
                Query(filterType: "dataSelectorHourly", downloadType: "dataSelectorMultiple"));

            Assert.Equal("https://s3/resolved.csv", result);
            Assert.Equal("NO2", capturedPollutant); // resolved from pollutant master, not the raw id
        }

        [Fact]
        public async Task GetAtomDataSelectionStation_ReturnsFailure_WhenHourlyDownloadTypeMissingAndS3Throws()
        {
            SetupBoundry(new List<SiteInfo> { Site("A") });
            _hourlyMock
                .Setup(h => h.GetAtomDataSelectionHourlyFetchService(
                    It.IsAny<List<SiteInfo>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData>());
            _s3Mock
                .Setup(s => s.WriteCsvToAwsS3BucketAsync(
                    It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>(), string.Empty))
                .ThrowsAsync(new AmazonS3Exception("s3 failure"));

            var result = await _sut.GetAtomDataSelectionStation(
                Query(filterType: "dataSelectorHourly", downloadType: null));

            Assert.Equal(Failure, result);
        }
    }
}