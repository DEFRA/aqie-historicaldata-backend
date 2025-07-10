namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public class AtomHistoryModel
    {
        public class PollutantDetails
        {
            public required string PollutantName { get; set; }
            public required string PollutantMasterUrl { get; set; }
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
            public decimal Total { get; set; }
            public string? DailyVerification { get; set; }
            public string? DailyValidity { get; set; }
            public string? DailyValue { get; set; }
            public string? DailyPollutantName { get; set; }
            public string? AnnualPollutantName { get; set; }
            public string? AnnualVerification { get; set; }
        }

        public class QueryStringData
        {
            public string? StationReadDate { get; set; }
            public string? Region { get; set; }
            public string? SiteType { get; set; }
            public string? SiteName { get; set; }
            public string? SiteId { get; set; }
            public string? Latitude { get; set; }
            public string? Longitude { get; set; }
            public string? Year { get; set; }
            public string? DownloadPollutant { get; set; }
            public string? DownloadPollutantType { get; set; }
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
    }
}
