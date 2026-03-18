using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public class AtomHistoryModel
    {
        public class PollutantDetails
        {
            public required string PollutantName { get; set; }
            public required string PollutantMasterUrl { get; set; }
            public string? stationCode { get; set; }
            public string? polygon { get; set; }
            public string? year { get; set; }
        }

        public class FinalData
        {
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public string? Verification { get; set; }
            public string? Validity { get; set; }
            public string? Value { get; set; }
            public string? PollutantName { get; set; }
            public string? ReportDate { get; set; }
            public decimal? Total { get; set; }
            public string? DailyVerification { get; set; }
            public string? DailyValidity { get; set; }
            public string? DailyValue { get; set; }
            public string? DailyPollutantName { get; set; }
            public string? AnnualPollutantName { get; set; }
            public string? AnnualVerification { get; set; }
            public string? StationName { get; set; }
            public string? SiteName { get; set; }
            public string? SiteType { get; set; }
            public string? Region { get; set; }
            public string? Country { get; set; }
        }

        public class QueryStringData
        {
            public string? StationReadDate { get; set; }
            public string? Region { get; set; }
            public string? regiontype { get; set; }
            public string? SiteType { get; set; }
            public string? SiteName { get; set; }
            public string? SiteId { get; set; }
            public string? Latitude { get; set; }
            public string? Longitude { get; set; }
            public string? Year { get; set; }
            public string? DownloadPollutant { get; set; }
            public string? DownloadPollutantType { get; set; }

            public string? dataSource { get; set; }
            public string? pollutantName { get; set; }
            public string? dataselectorfiltertype { get; set; }
            public string? dataselectordownloadtype { get; set; }
            public string? jobId { get; set; }
            public string? email { get; set; }
        }

        public class PivotPollutant
        {
            public string? Date { get; set; }
            public string? Time { get; set; }
            public List<SubPollutantItem>? SubPollutant { get; set; }
        }

        public class SubPollutantItem
        {
            public string? PollutantName { get; set; }
            public string? PollutantValue { get; set; }
            public string? Verification { get; set; }
        }

        public class FinalDataCsv
        {
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public string? Status { get; set; }
            public string? Unit { get; set; }
            public string? Value { get; set; }
            public string? PollutantName { get; set; }
            public string? SiteName { get; set; }
            public string? SiteType { get; set; }
            public string? Region { get; set; }
            public string? Country { get; set; }

        }
        public class PollutantInfo
        {
            public string? Name { get; set; }
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
        }

        public class SiteInfo
        {
            public string? SiteName { get; init; }
            public string? LocalSiteId { get; init; }
            public string? AreaType { get; init; }
            public string? SiteType { get; init; }
            public string? ZoneRegion { get; init; }
            public string? Latitude { get; init; }
            public string? Longitude { get; init; }
            public List<PollutantInfo>? Pollutants { get; set; } = new List<PollutantInfo>();
            public string? Country { get; set; }
        }
        public class SiteInfoWithCountry
        {
            public SiteInfo? Site { get; set; }
            public string? Country { get; set; }
        }
        public sealed class JobItem
        {
            public string? JobId { get; init; } = string.Empty;
            public List<SiteInfo>? StationData { get; init; } = new();
            public string? PollutantName { get; init; } = string.Empty;
            public string? Year { get; init; } = string.Empty;
            public QueryStringData? Data { get; init; } = new();
            public string? DownloadType { get; init; } = string.Empty;
        }

        public sealed class JobDocument
        {
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string? Id { get; set; }

            [BsonElement("jobId")]
            public string JobId { get; set; } = string.Empty;

            [BsonElement("status")]
            public JobStatusEnum Status { get; set; }

            [BsonElement("startTime")]
            public DateTime StartTime { get; set; }

            [BsonElement("endTime")]
            public DateTime? EndTime { get; set; }

            [BsonElement("errorReason")]
            public string? ErrorReason { get; set; }

            [BsonElement("resultUrl")]
            public string? ResultUrl { get; set; }

            [BsonElement("createdAt")]
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            [BsonElement("updatedAt")]
            public DateTime? UpdatedAt { get; set; }
        }


        public enum JobStatusEnum
        {
            Pending,
            Processing,
            Completed,
            Failed
        }

        public sealed record JobInfoDto
        {
            public string? JobId { get; init; } = string.Empty;
            public string? Status { get; init; } = string.Empty;
            public string? ResultUrl { get; init; }
            public string? ErrorReason { get; init; }
            public DateTime? CreatedAt { get; init; }
            public DateTime? UpdatedAt { get; init; }
            public DateTime? StartTime { get; init; }
            public DateTime? EndTime { get; init; }
        }

        public class eMailJobDocument
        {
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string? Id { get; set; }
            [BsonElement("jobId")]
            public string JobId { get; set; } = string.Empty;
            [BsonElement("pollutantName")]
            public string PollutantName { get; set; } = string.Empty;
            [BsonElement("dataSource")]
            public string DataSource { get; set; } = string.Empty;
            [BsonElement("year")]
            public string Year { get; set; } = string.Empty;
            [BsonElement("region")]
            public string Region { get; set; } = string.Empty;
            [BsonElement("regiontype")]
            public string Regiontype { get; set; } = string.Empty;

            [BsonElement("dataselectorfiltertype")]
            public string Dataselectorfiltertype { get; set; } = string.Empty;
            [BsonElement("dataselectordownloadtype")]
            public string Dataselectordownloadtype { get; set; } = string.Empty;
            [BsonElement("status")]
            public JobStatusEnum Status { get; set; }
            [BsonElement("startTime")]
            public DateTime? StartTime { get; set; }
            [BsonElement("endTime")]
            public DateTime? EndTime { get; set; }
            [BsonElement("errorReason")]
            public string ErrorReason { get; set; } = string.Empty;
            [BsonElement("resultUrl")]
            public string ResultUrl { get; set; } = string.Empty;
            [BsonElement("email")]
            public string Email { get; set; } = string.Empty;
            [BsonElement("mailSent")]
            public bool? MailSent { get; set; }
            [BsonElement("createdAt")]
            public DateTime CreatedAt { get; set; }
            [BsonElement("updatedAt")]
            public DateTime? UpdatedAt { get; set; }
        }


        // Define model class matching your JSON structure
        public class LocalAuthority
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
        }

        public class Root
        {
            public Filters? filters { get; set; }
            public Pagination? pagination { get; set; }
            public List<DataItem>? data { get; set; }
            public string? region { get; set; }
            public List<Info>? info { get; set; }
        }

        public class Filters
        {
            public string? search_terms { get; set; }
            public int? search_year { get; set; }
            public string? items_per_page { get; set; }
        }

        public class Pagination
        {
            public int? totalRows { get; set; }
            public int? totalPages { get; set; }
            public string? pagenum { get; set; }
        }

        public class DataItem
        {
            [JsonProperty("LA ID")]
            public int LA_ID { get; set; }

            [JsonProperty("Unique ID")]
            public string? Unique_ID { get; set; }

            [JsonProperty("X OS Grid Reference")]
            public int X_OS_Grid_Reference { get; set; }

            [JsonProperty("Y OS Grid Reference")]
            public int Y_OS_Grid_Reference { get; set; }
        }

        public class Info
        {
            public int? result { get; set; }
            public int? result_code { get; set; }
        }

        public class LocalAuthoritiesRoot
        {
            public List<LocalAuthorityData>? data { get; set; }
        }

        public class LocalAuthorityData
        {
            [JsonProperty("LA ID")]
            public int LA_ID { get; set; }

            [JsonProperty("LA ONS ID")]
            public string? LA_ONS_ID { get; set; }

            [JsonProperty("Unique ID")]
            public string? Unique_ID { get; set; }

            [JsonProperty("X OS Grid Reference")]
            public int X_OS_Grid_Reference { get; set; }

            [JsonProperty("Y OS Grid Reference")]
            public int Y_OS_Grid_Reference { get; set; }

            [JsonProperty("Region")]
            public string? LA_REGION { get; set; }

            // Add converted coordinates
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }
    }
}