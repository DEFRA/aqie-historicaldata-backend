using Amazon.S3;
using Amazon.S3.Model;
using AqieHistoricaldataBackend.Atomfeed.Models;
using Hangfire.MemoryStorage.Database;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionStationService;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAuthService
    {
        Task<string?> GetTokenAsync(string email, string password);
    }
    public class AuthService : IAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string?> GetTokenAsync(string email, string password)
        {
            var client = _httpClientFactory.CreateClient("RicardoNewAPI");

            var payload = new { email, password };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/login_check", content);

            // Do not throw here — allow caller to handle null token or log details.
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // Try common token property names
            if (doc.RootElement.TryGetProperty("token", out var token)) return token.GetString();
            if (doc.RootElement.TryGetProperty("access_token", out var accessToken)) return accessToken.GetString();
            if (doc.RootElement.TryGetProperty("jwt", out var jwt)) return jwt.GetString();

            // Fallback: if root is a string
            if (doc.RootElement.ValueKind == JsonValueKind.String) return doc.RootElement.GetString();

            return null;
        }
    }
    public class AtomDataSelectionStationService(ILogger<HistoryexceedenceService> Logger,
        IHttpClientFactory httpClientFactory,
    IAtomDataSelectionStationBoundryService AtomDataSelectionStationBoundryService,
    IAtomDataSelectionHourlyFetchService AtomDataSelectionHourlyFetchService,
    IAWSS3BucketService AWSS3BucketService, IAuthService AuthService) : IAtomDataSelectionStationService
    {
        //public async Task<string> GetAtomDataSelectionStation(QueryString data)
        public async Task<string> GetAtomDataSelectionStation(string pollutantName, string datasource, string year, string region, string dataselectorfiltertype)
        {

            try
            {
                QueryString queryStringsdata = new QueryString();
                //queryStringsdata.Add("pollutantName", pollutantName);
                //queryStringsdata.Add("dataSource", datasource);
                //queryStringsdata.Add("Year", year);
                //queryStringsdata.Add("Region", region);
                //queryStringsdata.Add("dataselectorfiltertype", dataselectorfiltertype);
                //string datasource = queryStringsdata.dataSource = datasource;
                //string year = queryStringsdataqueryStringsdata.Year = year;
                //string region = data.Region =region;
                //string dataselectorfiltertype = queryStringsdata.dataselectorfiltertype = dataselectorfiltertype;

                //queryStringsdata = queryStringsdata.Add("pollutantName", pollutantName);
                //queryStringsdata = queryStringsdata.Add("dataSource", datasource);
                //queryStringsdata = queryStringsdata.Add("Year", year);
                //queryStringsdata = queryStringsdata.Add("Region", region);
                //queryStringsdata = queryStringsdata.Add("dataselectorfiltertype", dataselectorfiltertype);

                //// Map QueryString to AtomHistoryModel.QueryStringData
                //var queryStringData = new AtomHistoryModel.QueryStringData();

                var queryStringData = new AtomHistoryModel.QueryStringData
                {
                    pollutantName = pollutantName,
                    dataSource = datasource,
                    Year = year,
                    Region = region,
                    dataselectorfiltertype = dataselectorfiltertype
                };

                //string emailFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_EMAIL") ?? "";
                //string passwordFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_PASSWORD") ?? "";
                string emailFromConfig = "PFRijFKn";
                string passwordFromConfig = "oG8mBc@ssf&7K3";
                var token = await AuthService.GetTokenAsync(emailFromConfig, passwordFromConfig);
                if (string.IsNullOrEmpty(token))
                {
                    Logger.LogError("Auth failed - no token returned");
                    return "Failure";
                }

                var client = httpClientFactory.CreateClient("RicardoNewAPI");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = "api/site_meta_datas?with-closed=true&with-pollutants=1";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string responsebody = await response.Content.ReadAsStringAsync();

                var sitemetadatainfo = ParseSiteMeta(responsebody);

                // 2) Sites that have any pollutant whose name contains "Ozone" (covers "Ozone (O3)" etc.)
                var ozoneSitesContains = sitemetadatainfo
                    .Where(s => s.Pollutants?.Any(p => p.Name?.Contains("Ozone", StringComparison.OrdinalIgnoreCase) == true) == true)
                    .ToList();

                // 3) Flattened list of pollutant records with site metadata for matches (useful to access both)
                var ozoneRecords = sitemetadatainfo
                    .SelectMany(s => (s.Pollutants ?? Enumerable.Empty<PollutantInfo>())
                        .Where(p => p.Name != null && p.Name.Contains("Ozone", StringComparison.OrdinalIgnoreCase))
                        .Select(p => new { Site = s, Pollutant = p }))
                    .ToList();

                //var filterpollutantyear = ozoneRecords
                //    .Where(r => r.Pollutant.StartDate != null && r.Pollutant.StartDate.StartsWith(year) ||
                //                r.Pollutant.EndDate != null && r.Pollutant.EndDate.StartsWith(year))
                //    .ToList();

                var filterpollutantyear = ozoneRecords
                .Where(r =>
                {
                    DateTime startDate, endDate;
                    bool hasStartDate = DateTime.TryParseExact(r.Pollutant.StartDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out startDate);
                    bool hasEndDate = DateTime.TryParseExact(r.Pollutant.EndDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out endDate);

                    if (int.TryParse(year, out int yearInt))
                    {
                        var yearStart = new DateTime(yearInt, 1, 1);
                        var yearEnd = new DateTime(yearInt, 12, 31);

                        if (hasStartDate && hasEndDate)
                        {
                            return startDate <= yearEnd && endDate >= yearStart;
                        }
                        else if (hasStartDate && !hasEndDate)
                        {
                            // Treat null endDate as ongoing
                            return startDate <= yearEnd;
                        }
                    }
                    return false;
                })
                .ToList();

                //var client = httpClientFactory.CreateClient("RicardoNewAPI");
                //var url = $"api/site_meta_datas?with-closed=true&with-pollutants=1";
                //var response = await client.GetAsync(url);
                //response.EnsureSuccessStatusCode();

                //string responsebody = await response.content.readasstringasync();

                //For Atom feed data
                //var client1 = httpClientFactory.CreateClient("Atomfeed");
                //var url1 = $"data/atom-dls/auto/{year}/atom.en.xml";
                //var response1 = await client1.GetAsync(url1);
                //response1.EnsureSuccessStatusCode();

                //var xmlContent = await response1.Content.ReadAsStringAsync();
                //XDocument doc = XDocument.Parse(xmlContent);
                //XNamespace atom = "http://www.w3.org/2005/Atom";
                //XNamespace georss = "http://www.georss.org/georss";
                //var entries = doc.Descendants(atom + "entry");
                //var allPollutants = new List<PollutantDetails>();
                //var finalallpollutatns = new List<PollutantDetails>();

                //foreach (var entry in entries)
                //{
                //    var title = entry.Element(atom + "title")?.Value;
                //    string stationCode = string.Empty;
                //    var polygonElement = entry.Element(georss + "polygon");
                //    string polygonCoordinates = polygonElement?.Value;
                //    string coordinatesresult = string.Empty;

                //    if (!string.IsNullOrEmpty(title))
                //    {
                //        var match = System.Text.RegularExpressions.Regex.Match(title, @"\(([^)]+)\)");
                //        if (match.Success)
                //        {
                //            stationCode = match.Groups[1].Value;
                //        }
                //    }

                //    //List<(double lat, double lon)> coordinates = new List<(double, double)>();

                //    //if (!string.IsNullOrEmpty(polygonCoordinates))
                //    //{
                //    //    var parts = polygonCoordinates.Split(' ');
                //    //    for (int i = 0; i < parts.Length - 1; i += 2)
                //    //    {
                //    //        if (double.TryParse(parts[i], out double lat) && double.TryParse(parts[i + 1], out double lon))
                //    //        {
                //    //            coordinates.Add((lat, lon));
                //    //        }
                //    //    }
                //    //}
                //    if (!string.IsNullOrEmpty(polygonCoordinates))
                //    {
                //        string[] coordinates = polygonCoordinates.Split(' ');
                //        coordinatesresult = $"{coordinates[0]},{coordinates[1]}";
                //    }

                //    var relatedLinks = entry.Elements(atom + "link")
                //                        .Where(l => l.Attribute("rel")?.Value == "related");

                //    foreach (var link in relatedLinks)
                //    {
                //        var pollutantTitle = link.Attribute("title")?.Value;
                //        var pollutantHref = link.Attribute("href")?.Value;

                //        if (!string.IsNullOrEmpty(pollutantHref))
                //        {
                //            pollutantHref = pollutantHref.Replace("http://", "").Replace("https://", "");
                //        }

                //        if (!string.IsNullOrEmpty(pollutantTitle) && !string.IsNullOrEmpty(pollutantHref))
                //        {
                //            var nameParts = pollutantTitle.Split(new[] { "Pollutant in feed - " }, StringSplitOptions.None);
                //            var pollutantNameurl = nameParts.Length > 1 ? nameParts[1] : pollutantTitle;

                //            allPollutants.Add(new PollutantDetails
                //            {
                //                stationCode = stationCode,
                //                PollutantName = pollutantNameurl,
                //                PollutantMasterUrl = pollutantHref,
                //                polygon = coordinatesresult,
                //                year = year
                //            });
                //        }
                //    }
                //    finalallpollutatns.AddRange(allPollutants);
                //}

                //var targetUrls = new List<string>
                //    {
                //        "dd.eionet.europa.eu/vocabulary/aq/pollutant/8",
                //        "dd.eionet.europa.eu/vocabulary/aq/pollutant/5",
                //        "dd.eionet.europa.eu/vocabulary/aq/pollutant/6001",
                //        "dd.eionet.europa.eu/vocabulary/aq/pollutant/7",
                //        "dd.eionet.europa.eu/vocabulary/aq/pollutant/1"
                //    };

                //var filteredPollutants = allPollutants
                //    .Where(p => targetUrls.Contains(p.PollutantMasterUrl))
                //    .GroupBy(p => new { p.PollutantName, p.PollutantMasterUrl })
                //    .Select(g => g.First())
                //    .ToList();

                //var uniquePollutants = allPollutants
                //    .GroupBy(p => new { p.PollutantName, p.PollutantMasterUrl })
                //    .Select(g => g.First())
                //    .ToList();

                //var uniquestationCode = allPollutants
                //        .GroupBy(p => new { p.stationCode })
                //        .Select(g => g.First())
                //        //.Take(50)
                //        .ToList();

                ////var filtered = allPollutants.Where(p => p.PollutantName == filter);
                //var filtered = uniquePollutants.Where(p => p.PollutantName == pollutantName);
                //var filtered_station_pollutant = uniquestationCode.Where(p => p.PollutantName == pollutantName).ToList();
                ////var filtered = filteredPollutants.Where(p => p.PollutantName == filter);
                ////var resultfiltered = filtered.Any() ? filtered.ToList() : allPollutants;
                //var resultfiltered = filtered.Any() ? filtered.ToList() : filteredPollutants;

                //var stationData = await AtomDataSelectionStationBoundryService.GetAtomDataSelectionStationBoundryService(filtered_station_pollutant);
                var stationData = await AtomDataSelectionStationBoundryService.GetAtomDataSelectionStationBoundryService(filterpollutantyear.Select(r => r.Site).ToList());

                var stationcountresult = stationData.Count();

                if (dataselectorfiltertype == "dataSelectorCount")
                {
                    return stationcountresult.ToString();
                }
                else
                {
                    string PresignedUrl = string.Empty;
                    var stationlistdata = await AtomDataSelectionHourlyFetchService.GetAtomDataSelectionHourlyFetchService(stationData, pollutantName, year);
                    PresignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(stationlistdata, queryStringData, dataselectorfiltertype);
                    return PresignedUrl.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in GetAtomDataSelectionStation {Error}", ex.Message);
                Logger.LogError("Error in GetAtomDataSelectionStation {Error}", ex.StackTrace);
                return "Failure";
            }

        }  
    private static List<SiteInfo> ParseSiteMeta(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            List<SiteInfo> siteList = new List<SiteInfo>();

            // Fix: Get the "member" property as a JsonElement, then enumerate its array items
            if (root.TryGetProperty("member", out var memberElement) && memberElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var site in memberElement.EnumerateArray())
                {
                    var siteInfo = new SiteInfo
                    {
                        SiteName = site.TryGetProperty("siteName", out var siteNameEl) ? siteNameEl.ToString() : null,
                        LocalSiteId = site.TryGetProperty("localSiteId", out var localSiteIdEl) ? localSiteIdEl.ToString() : null,
                        AreaType = site.TryGetProperty("areaType", out var areaTypeEl) ? areaTypeEl.ToString() : null,
                        SiteType = site.TryGetProperty("siteType", out var siteTypeEl) ? siteTypeEl.ToString() : null,
                        ZoneRegion = site.TryGetProperty("zoneRegion", out var zoneRegionEl) ? zoneRegionEl.ToString() : null,
                        Latitude = site.TryGetProperty("latitude", out var latitudeEl) ? latitudeEl.ToString() : null,
                        Longitude = site.TryGetProperty("longitude", out var longitudeEl) ? longitudeEl.ToString() : null,
                        Pollutants = new List<PollutantInfo>()
                    };

                    // Fix: Get "pollutantsMetaData" as a JsonElement and enumerate its properties
                    if (site.TryGetProperty("pollutantsMetaData", out var pollutantsMetaDataEl) && pollutantsMetaDataEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var pollutantProp in pollutantsMetaDataEl.EnumerateObject())
                        {
                            var data = pollutantProp.Value;
                            var pollutantInfo = new PollutantInfo
                            {
                                Name = data.TryGetProperty("name", out var nameEl) ? nameEl.ToString() : null,
                                StartDate = data.TryGetProperty("startDate", out var startDateEl) ? startDateEl.ToString() : null,
                                EndDate = data.TryGetProperty("endDate", out var endDateEl) ? endDateEl.ToString() : null
                            };
                            siteInfo.Pollutants.Add(pollutantInfo);
                        }
                    }

                    siteList.Add(siteInfo);
                }
            }

            return siteList;
        }
    }
}
