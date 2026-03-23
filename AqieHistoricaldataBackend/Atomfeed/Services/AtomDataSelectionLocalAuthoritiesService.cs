using Microsoft.AspNetCore.Mvc.RazorPages;
using NetTopologySuite.Index.HPRtree;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using System.Text.Json;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
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
                var selectedlocalAuthorityId = region.Split(',')
                        .Select(r => r.Trim())
                        .ToList();

                List<LocalAuthorityData> localAuthoritiesfinallist = new List<LocalAuthorityData>();

                var laClient = httpClientFactory.CreateClient("laqmAPI");

                laClient.DefaultRequestHeaders.Add("X-API-Key", Environment.GetEnvironmentVariable("LAQM_API_KEY"));
                laClient.DefaultRequestHeaders.Add("X-API-PartnerId", Environment.GetEnvironmentVariable("LAQM_USERID"));

                var localauthorities = $"/xapi/getLocalAuthorities/json";
                var localauthoritiesresponse = await laClient.GetStringAsync(localauthorities);

                var allLocalAuthorities = JsonConvert.DeserializeObject<LocalAuthoritiesRoot>(localauthoritiesresponse);

                // Dictionary<int, string?> to match the nullable LA_REGION value
                var regionLookup = allLocalAuthorities?.data?
                    .GroupBy(la => la.LA_ID)
                    .ToDictionary(g => g.Key, g => g.Last().LA_REGION)
                    ?? new Dictionary<int, string?>();

                foreach (var laId in selectedlocalAuthorityId)
                {
                    Logger.LogInformation("Selected From frontend API LA ID: {LaId}", laId);

                    string year = (DateTime.Now.Year - 1).ToString();
                    string localAuthorityId = laId;
                    string pageNumber = "1";
                    string itemsPerPage = "100";

                    var laUrl = $"/xapi/getSingleDTDataByYear/{year}/{localAuthorityId}/{pageNumber}/{itemsPerPage}/json";
                    var laResponse = await laClient.GetStringAsync(laUrl);

                    var localAuthorities = JsonConvert.DeserializeObject<LocalAuthoritiesRoot>(laResponse);
                    if (localAuthorities?.data != null)
                    {
                        foreach (var la in localAuthorities.data)
                        {
                            Logger.LogInformation("From LAQM API LA ID: {LaId}", la.LA_ID);

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

                            if (regionLookup.TryGetValue(la.LA_ID, out var laRegion))
                            {
                                la.LA_REGION = laRegion;
                            }
                        }
                        localAuthoritiesfinallist.AddRange(localAuthorities.data);
                    }
                }

                return localAuthoritiesfinallist;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GetAtomDataSelectionStationBoundryService");
                throw;
            }
        }
    }
}
