using Newtonsoft.Json.Linq;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public static class AtomFeedHelper
    {
        /// <summary>
        /// Loads an XML stream and returns the gml:featureMember JArray,
        /// or an empty JArray if the element is absent.
        /// </summary>
        public static JArray ParseXmlStreamToFeatureArray(Stream stream)
        {
            var xml = new XmlDocument();
            xml.Load(stream);
            var json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xml);
            return JObject.Parse(json)["gml:FeatureCollection"]?["gml:featureMember"] as JArray
                   ?? new JArray();
        }

        /// <summary>
        /// Extracts the pollutant URL segment from a full xlink:href value.
        /// </summary>
        public static string? ExtractPollutantId(string? href)
            => href is not null ? href[(href.LastIndexOf('/') + 1)..] : null;

        /// <summary>
        /// Splits a raw swe:values string into validated part arrays (>= 5 columns).
        /// </summary>
        public static IEnumerable<string[]> SplitSweValues(string values)
            => values.Replace("\r\n", "").Trim().Split("@@")
                     .Select(item => item.Split(','))
                     .Where(parts => parts.Length >= 5);

        /// <summary>
        /// Maps part arrays to FinalData without site context (used by AtomHourlyFetchService).
        /// </summary>
        public static List<FinalData> ToFinalData(IEnumerable<string[]> rows, string pollutantName)
            => rows.Select(parts => new FinalData
            {
                StartTime    = parts[0],
                EndTime      = parts[1],
                Verification = parts[2],
                Validity     = parts[3],
                Value        = parts[4],
                PollutantName = pollutantName
            }).ToList();

        /// <summary>
        /// Maps part arrays to FinalData with site context (used by AtomDataSelectionHourlyFetchService).
        /// </summary>
        public static List<FinalData> ToFinalData(IEnumerable<string[]> rows, string pollutantName, SiteInfo siteInfo)
            => rows.Select(parts => new FinalData
            {
                StartTime    = parts[0],
                EndTime      = parts[1],
                Verification = parts[2],
                Validity     = parts[3],
                Value        = parts[4],
                PollutantName = pollutantName,
                SiteName  = siteInfo.SiteName,
                SiteType  = siteInfo.AreaType is null && siteInfo.SiteType is null
                                ? null
                                : (siteInfo.AreaType ?? "") + (siteInfo.SiteType ?? ""),
                Region    = siteInfo.ZoneRegion,
                Country   = siteInfo.Country
            }).ToList();
    }
}