using Hangfire.MemoryStorage.Database;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetTopologySuite.Index.HPRtree;
using NetTopologySuite.Utilities;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using RTools_NTS.Util;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Newtonsoft.Json;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionLocalAuthoritiesService(ILogger<HistoryexceedenceService> Logger,
IHttpClientFactory httpClientFactory) : IAtomDataSelectionLocalAuthoritiesService
    {
        public async Task<List<LocalAuthorityData>> GetAtomDataSelectionLocalAuthoritiesService(string region)
        {
            try
            {
                //var localAuthorities = new List<LocalAuthoritiesRoot>();


                /// xapi / getSingleDTDataByYear / 2025 / 1 / 1 / 100 / json
                //[integer]: Reporting year for required content
                //[integer]: Local Authority ID for required content
                //[integer]: Pagination - page number required
                //[integer]: Pagination - no.of items per page
                //[text] Return data in format[json, xml, php]

                var selectedlocalAuthorityId = region.Split(',')
                        .Select(r => r.Trim())
                        .ToList();

                List<LocalAuthorityData> localAuthoritiesfinallist = new List<LocalAuthorityData>();

                foreach (var laId in selectedlocalAuthorityId)
                {
                    Logger.LogInformation($"Selected From frontend API LA ID: {laId}");


                    string year = DateTime.Now.Year.ToString();
                    string localAuthorityId = laId;
                    string pageNumber = "1";
                    string itemsPerPage = "100";
                    //string url = $"/xapi/getSingleDTDataByYear/{year}/{localAuthorityId}/{pageNumber}/{itemsPerPage}/json";



                    // Fetch Local Authorities data
                    var laClient = httpClientFactory.CreateClient("laqmAPI");
                    laClient.DefaultRequestHeaders.Add("X-API-Key", Environment.GetEnvironmentVariable("LAQM_API_KEY"));
                    laClient.DefaultRequestHeaders.Add("X-API-PartnerId", Environment.GetEnvironmentVariable("LAQM_USERID"));

                    var laUrl = $"/xapi/getSingleDTDataByYear/{year}/{localAuthorityId}/{pageNumber}/{itemsPerPage}/json";
                    var laResponse = await laClient.GetStringAsync(laUrl);

                    var localAuthorities = JsonConvert.DeserializeObject<LocalAuthoritiesRoot>(laResponse);
                    if (localAuthorities.data != null)
                    {
                        // Log the local authorities data
                        foreach (var la in localAuthorities.data)
                        {
                            Logger.LogInformation($"From LAQM API LA ID: {la.LA_ID}");

                            double easting = la.X_OS_Grid_Reference;
                            double northing = la.Y_OS_Grid_Reference;

                            var csFactory = new CoordinateSystemFactory();
                            var ctFactory = new CoordinateTransformationFactory();

                            var sourceCS = csFactory.CreateFromWkt(
                                "PROJCS[\"OSGB 1936 / British National Grid\", " +
                                "GEOGCS[\"OSGB 1936\", DATUM[\"OSGB_1936\", SPHEROID[\"Airy 1830\",6377563.396,299.3249646]], " +
                                "PRIMEM[\"Greenwich\",0], UNIT[\"degree\",0.0174532925199433]], " +
                                "PROJECTION[\"Transverse_Mercator\"], PARAMETER[\"latitude_of_origin\",49], " +
                                "PARAMETER[\"central_meridian\",-2], PARAMETER[\"scale_factor\",0.9996012717], " +
                                "PARAMETER[\"false_easting\",400000], PARAMETER[\"false_northing\",-100000], UNIT[\"metre\",1]]");

                            var targetCS = csFactory.CreateFromWkt(
                                "GEOGCS[\"WGS 84\", DATUM[\"WGS_1984\", SPHEROID[\"WGS 84\",6378137,298.257223563]], " +
                                "PRIMEM[\"Greenwich\",0], UNIT[\"degree\",0.0174532925199433]]");

                            var transform = ctFactory.CreateFromCoordinateSystems(sourceCS, targetCS);
                            double[] point = transform.MathTransform.Transform(new double[] { easting, northing });

                            la.Latitude = point[1];
                            la.Longitude = point[0];
                            //// Check if this local authority matches any site in the filtered_station_pollutant list
                            //var matchingSites = filtered_station_pollutant
                            //    .Where(site => site. == la.LA_ID)
                            //    .ToList();
                        }
                        localAuthoritiesfinallist.AddRange(localAuthorities.data);
                    }
                }

                //var root = JsonConvert.DeserializeObject<Root>(laResponse);
                return localAuthoritiesfinallist;

            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in GetAtomDataSelectionStationBoundryService: {ex.Message}");
                throw;
            }
        }
    }
    
    // Define model class matching your JSON structure
    public class LocalAuthority
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        // Add other properties as needed based on actual JSON response
    }

    public class Root
    {
        public Filters filters { get; set; }
        public Pagination pagination { get; set; }
        public List<DataItem> data { get; set; }
        public string region { get; set; }
        public List<Info> info { get; set; }
    }

    public class Filters
    {
        public string search_terms { get; set; }
        public int search_year { get; set; }
        public string items_per_page { get; set; }
    }

    public class Pagination
    {
        public int totalRows { get; set; }
        public int totalPages { get; set; }
        public string pagenum { get; set; }
    }

    public class DataItem
    {
        [JsonProperty("LA ID")]
        public int LA_ID { get; set; }

        [JsonProperty("Unique ID")]
        public string Unique_ID { get; set; }

        [JsonProperty("X OS Grid Reference")]
        public int X_OS_Grid_Reference { get; set; }

        [JsonProperty("Y OS Grid Reference")]
        public int Y_OS_Grid_Reference { get; set; }
    }

    public class Info
    {
        public int result { get; set; }
        public int result_code { get; set; }
    }
    public class LocalAuthoritiesRoot
    {
        public List<LocalAuthorityData> data { get; set; }
    }

    public class LocalAuthorityData
    {
        [JsonProperty("LA ID")]
        public int LA_ID { get; set; }

        [JsonProperty("LA ONS ID")]
        public string LA_ONS_ID { get; set; }

        [JsonProperty("Unique ID")]
        public string Unique_ID { get; set; }

        [JsonProperty("X OS Grid Reference")]
        public int X_OS_Grid_Reference { get; set; }

        [JsonProperty("Y OS Grid Reference")]
        public int Y_OS_Grid_Reference { get; set; }

        // Add converted coordinates
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
