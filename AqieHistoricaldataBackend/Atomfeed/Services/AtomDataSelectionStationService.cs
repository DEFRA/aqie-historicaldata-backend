using Amazon.S3;
using Amazon.S3.Model;
using AqieHistoricaldataBackend.Atomfeed.Models;
using Hangfire.MemoryStorage.Database;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionStationService;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionStationService(ILogger<HistoryexceedenceService> Logger, 
        IHttpClientFactory httpClientFactory,
    IAtomDataSelectionStationBoundryService AtomDataSelectionStationBoundryService,
    IAtomDataSelectionHourlyFetchService AtomDataSelectionHourlyFetchService,
    IAWSS3BucketService AWSS3BucketService) : IAtomDataSelectionStationService
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

                var client = httpClientFactory.CreateClient("Atomfeed");
                var url = $"data/atom-dls/auto/{year}/atom.en.xml";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var xmlContent = await response.Content.ReadAsStringAsync();
                XDocument doc = XDocument.Parse(xmlContent);
                XNamespace atom = "http://www.w3.org/2005/Atom";
                XNamespace georss = "http://www.georss.org/georss";
                var entries = doc.Descendants(atom + "entry");
                var allPollutants = new List<PollutantDetails>();
                var finalallpollutatns = new List<PollutantDetails>();

                foreach (var entry in entries)
                {
                    var title = entry.Element(atom + "title")?.Value;
                    string stationCode = string.Empty; 
                    var polygonElement = entry.Element(georss + "polygon");
                    string polygonCoordinates = polygonElement?.Value;
                    string coordinatesresult = string.Empty;

                    if (!string.IsNullOrEmpty(title))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(title, @"\(([^)]+)\)");
                        if (match.Success)
                        {
                            stationCode = match.Groups[1].Value;
                        }
                    }

                    //List<(double lat, double lon)> coordinates = new List<(double, double)>();

                    //if (!string.IsNullOrEmpty(polygonCoordinates))
                    //{
                    //    var parts = polygonCoordinates.Split(' ');
                    //    for (int i = 0; i < parts.Length - 1; i += 2)
                    //    {
                    //        if (double.TryParse(parts[i], out double lat) && double.TryParse(parts[i + 1], out double lon))
                    //        {
                    //            coordinates.Add((lat, lon));
                    //        }
                    //    }
                    //}
                    if (!string.IsNullOrEmpty(polygonCoordinates))
                    {
                        string[] coordinates = polygonCoordinates.Split(' ');
                        coordinatesresult = $"{coordinates[0]},{coordinates[1]}";
                    }

                        var relatedLinks = entry.Elements(atom + "link")
                                            .Where(l => l.Attribute("rel")?.Value == "related");

                    foreach (var link in relatedLinks)
                    {
                        var pollutantTitle = link.Attribute("title")?.Value;
                        var pollutantHref = link.Attribute("href")?.Value;

                        if (!string.IsNullOrEmpty(pollutantHref))
                        {
                            pollutantHref = pollutantHref.Replace("http://", "").Replace("https://", "");
                        }

                        if (!string.IsNullOrEmpty(pollutantTitle) && !string.IsNullOrEmpty(pollutantHref))
                        {
                            var nameParts = pollutantTitle.Split(new[] { "Pollutant in feed - " }, StringSplitOptions.None);
                            var pollutantNameurl = nameParts.Length > 1 ? nameParts[1] : pollutantTitle;

                            allPollutants.Add(new PollutantDetails
                            {
                                stationCode = stationCode,
                                PollutantName = pollutantNameurl,
                                PollutantMasterUrl = pollutantHref,
                                polygon = coordinatesresult,
                                year = year
                            });
                        }
                    }
                    finalallpollutatns.AddRange(allPollutants);
                }

                var targetUrls = new List<string>
                    {
                        "dd.eionet.europa.eu/vocabulary/aq/pollutant/8",
                        "dd.eionet.europa.eu/vocabulary/aq/pollutant/5",
                        "dd.eionet.europa.eu/vocabulary/aq/pollutant/6001",
                        "dd.eionet.europa.eu/vocabulary/aq/pollutant/7",
                        "dd.eionet.europa.eu/vocabulary/aq/pollutant/1"
                    };

                var filteredPollutants = allPollutants
                    .Where(p => targetUrls.Contains(p.PollutantMasterUrl))
                    .GroupBy(p => new { p.PollutantName, p.PollutantMasterUrl })
                    .Select(g => g.First())
                    .ToList();

                var uniquePollutants = allPollutants
                    .GroupBy(p => new { p.PollutantName, p.PollutantMasterUrl })
                    .Select(g => g.First())
                    .ToList();

                var uniquestationCode = allPollutants
                        .GroupBy(p => new { p.stationCode })
                        .Select(g => g.First())
                        //.Take(50)
                        .ToList();

                //var filtered = allPollutants.Where(p => p.PollutantName == filter);
                var filtered = uniquePollutants.Where(p => p.PollutantName == pollutantName);
                var filtered_station_pollutant = uniquestationCode.Where(p => p.PollutantName == pollutantName).ToList();
                //var filtered = filteredPollutants.Where(p => p.PollutantName == filter);
                //var resultfiltered = filtered.Any() ? filtered.ToList() : allPollutants;
                var resultfiltered = filtered.Any() ? filtered.ToList() : filteredPollutants;

                var stationcountData = await AtomDataSelectionStationBoundryService.GetAtomDataSelectionStationBoundryService(filtered_station_pollutant);

                var stationcountresult = stationcountData.Count(); 

                if(dataselectorfiltertype == "dataSelectorCount")
                {
                    return stationcountresult.ToString();
                }
                else
                {
                    string PresignedUrl = string.Empty;
                    var stationlistdata = await AtomDataSelectionHourlyFetchService.GetAtomDataSelectionHourlyFetchService(stationcountData, pollutantName);
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
    }
}
