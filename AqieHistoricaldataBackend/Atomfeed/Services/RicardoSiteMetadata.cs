using System.Net.Http.Headers;
using System.Text.Json;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Handles retrieval and parsing of Ricardo site metadata.
    /// </summary>
    public static class RicardoSiteMetadata
    {
        public static async Task<List<SiteInfo>> FetchSiteMetadata(IHttpClientFactory httpClientFactory, string token)
        {
            var client = httpClientFactory.CreateClient("RicardoNewAPI");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = "api/site_meta_datas?with-closed=true&with-pollutants=1";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string responsebody = await response.Content.ReadAsStringAsync();
            return ParseSiteMeta(responsebody);
        }

        public static List<SiteInfo> ParseSiteMeta(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            List<SiteInfo> siteList = new List<SiteInfo>();

            if (root.TryGetProperty("member", out var memberElement) && memberElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var site in memberElement.EnumerateArray())
                {
                    var siteInfo = ParseSingleSite(site);
                    siteList.Add(siteInfo);
                }
            }

            return siteList;
        }

        private static SiteInfo ParseSingleSite(JsonElement site)
        {
            var siteInfo = new SiteInfo
            {
                SiteName = site.TryGetProperty("siteName", out var siteNameEl) ? siteNameEl.ToString() : null,
                LocalSiteId = site.TryGetProperty("localSiteId", out var localSiteIdEl) ? localSiteIdEl.ToString() : null,
                AreaType = site.TryGetProperty("areaType", out var areaTypeEl) ? areaTypeEl.ToString() : null,
                SiteType = site.TryGetProperty("siteType", out var siteTypeEl) ? siteTypeEl.ToString() : null,
                ZoneRegion = site.TryGetProperty("governmentRegion", out var zoneRegionEl) ? zoneRegionEl.ToString() : null,
                RegionId = site.TryGetProperty("zoneRegion", out var regionIdEl) ? regionIdEl.ToString() : null,
                Latitude = site.TryGetProperty("latitude", out var latitudeEl) ? latitudeEl.ToString() : null,
                Longitude = site.TryGetProperty("longitude", out var longitudeEl) ? longitudeEl.ToString() : null,
                Pollutants = ParsePollutantsMetaData(site)
            };

            return siteInfo;
        }

        private static List<PollutantInfo> ParsePollutantsMetaData(JsonElement site)
        {
            var pollutants = new List<PollutantInfo>();

            if (site.TryGetProperty("pollutantsMetaData", out var pollutantsMetaDataEl) && pollutantsMetaDataEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var pollutantProp in pollutantsMetaDataEl.EnumerateObject())
                {
                    var pollutantInfo = ParseSinglePollutant(pollutantProp.Value);
                    pollutants.Add(pollutantInfo);
                }
            }

            return pollutants;
        }
        private static PollutantInfo ParseSinglePollutant(JsonElement data)
        {
            return new PollutantInfo
            {
                Name = data.TryGetProperty("name", out var nameEl) ? nameEl.ToString() : null,
                StartDate = data.TryGetProperty("startDate", out var startDateEl) ? startDateEl.ToString() : null,
                EndDate = data.TryGetProperty("endDate", out var endDateEl) ? endDateEl.ToString() : null
            };
        }
    }
}