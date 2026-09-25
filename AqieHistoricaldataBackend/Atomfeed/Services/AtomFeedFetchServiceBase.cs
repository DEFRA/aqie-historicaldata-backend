using Newtonsoft.Json.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Outcome of an upstream feed fetch. <paramref name="UpstreamFailed"/> separates "the feed
    /// could not be read" from "the feed was read and holds nothing", which callers need in order
    /// to return a meaningful status code.
    /// </summary>
    public sealed record AtomFeedFetchResult(JArray Features, bool UpstreamFailed)
    {
        public static AtomFeedFetchResult Ok(JArray features) => new(features, false);
        public static AtomFeedFetchResult NoData() => new(new JArray(), false);
        public static AtomFeedFetchResult Failed() => new(new JArray(), true);
    }

    public abstract class AtomFeedFetchServiceBase(IHttpClientFactory httpClientFactory)
    {
        protected abstract ILogger Logger { get; }

        protected async Task<JArray> FetchAtomFeedAsync(string? siteID, string year, string? dataSource = null)
            => (await FetchAtomFeedResultAsync(siteID, year, dataSource)).Features;

        protected async Task<AtomFeedFetchResult> FetchAtomFeedResultAsync(string? siteID, string year, string? dataSource = null)
        {
            if (string.IsNullOrWhiteSpace(siteID) || string.IsNullOrWhiteSpace(year))
            {
                Logger.LogWarning("Invalid FetchAtomFeedAsync siteID or year: siteID='{SiteID}', year='{Year}'", siteID, year);
                return AtomFeedFetchResult.NoData();
            }

            var client = httpClientFactory.CreateClient("Atomfeed");
            var path = dataSource == "AURN" || dataSource == null
                ? $"data/atom-dls/observations/auto/GB_FixedObservations_{year}_{siteID}.xml"
                : $"data/atom-dls/observations/non-auto/GB_FixedObservations_{year}_{siteID}.xml";

            try
            {
                Logger.LogInformation("Fetching Atom feed for site {SiteID} year {Year} at {DateTime}", siteID, year, DateTime.Now);
                var response = await client.GetAsync(path);
                Logger.LogInformation("Received Atom feed response for site {SiteID} year {Year}: {StatusCode}", siteID, year, (int)response.StatusCode);

                var shortCircuit = await ClassifyResponseAsync(response, path, siteID, year);
                if (shortCircuit is not null)
                    return shortCircuit;

                var stream = await response.Content.ReadAsStreamAsync();
                return AtomFeedFetchResult.Ok(AtomFeedHelper.ParseXmlStreamToFeatureArray(stream));
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Logger.LogWarning(ex, "Atom feed not found (404) for URL: {Url} (siteID: {SiteID}, year: {Year})", path, siteID, year);
                    return AtomFeedFetchResult.NoData();
                }

                Logger.LogError(ex, "HTTP error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", path, siteID, year, ex.Message);
                return AtomFeedFetchResult.Failed();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", path, siteID, year, ex.Message);
                return AtomFeedFetchResult.Failed();
            }
        }

        /// <summary>Returns null when the response should be parsed, otherwise the outcome to report.</summary>
        private async Task<AtomFeedFetchResult?> ClassifyResponseAsync(HttpResponseMessage response, string path, string? siteID, string year)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                Logger.LogWarning("Server returned 304 Not Modified for site {SiteID} year {Year}", siteID, year);
                return AtomFeedFetchResult.Failed();
            }

            // A missing feed file means this station published nothing for this year.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Logger.LogWarning("Atom feed not found (404) for URL: {Url} (siteID: {SiteID}, year: {Year})", path, siteID, year);
                return AtomFeedFetchResult.NoData();
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogWarning("HTTP {StatusCode} when fetching Atom feed for site {SiteID} year {Year}. Response: {Response}",
                    (int)response.StatusCode, siteID, year, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.PreconditionRequired)
                    Logger.LogError("Server returned 428 Precondition Required. Check if User-Agent, cookies, or other headers are needed.");

                return AtomFeedFetchResult.Failed();
            }

            return null;
        }

        protected List<FinalData> ProcessAtomData(JArray features, List<PollutantDetails> pollutants, SiteInfo? siteinfo = null)
        {
            var finalList = new List<FinalData>();
            if (features == null || features.Count == 0)
                return finalList;

            for (int i = 1; i < features.Count; i++)
            {
                ProcessSingleFeature(features[i], pollutants, siteinfo, finalList);
            }

            return finalList;
        }

        private void ProcessSingleFeature(JToken feature, List<PollutantDetails> pollutants, SiteInfo? siteinfo, List<FinalData> finalList)
        {
            try
            {
                var href = feature["om:OM_Observation"]?["om:observedProperty"]?["@xlink:href"]?.ToString();
                if (string.IsNullOrEmpty(href)) return;

                string? cleanedUrl = AtomFeedHelper.ExtractPollutantId(href);
                var match = pollutants.FirstOrDefault(p => p.PollutantMasterUrl == cleanedUrl);
                if (match == null) return;

                var values = feature["om:OM_Observation"]?["om:result"]?["swe:DataArray"]?["swe:values"]?.ToString();
                if (string.IsNullOrEmpty(values)) return;

                var rows = AtomFeedHelper.SplitSweValues(values);
                finalList.AddRange(siteinfo != null
                    ? AtomFeedHelper.ToFinalData(rows, match.PollutantName, siteinfo)
                    : AtomFeedHelper.ToFinalData(rows, match.PollutantName));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing ProcessAtomData feature member");
            }
        }
    }
}