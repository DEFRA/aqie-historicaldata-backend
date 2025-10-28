namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public class AtomHistoryModel
    {
        public class PollutantDetails
        {
            public required string PollutantName { get; set; }
            public required string PollutantMasterUrl { get; set; }
            public string stationCode { get; set; }
            public string polygon { get; set; }
            public string year { get; set; }
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
            public string Name { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
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
            //public string? Name { get; init; }
            //public string? StartDate { get; init; }
            //public string? EndDate { get; init; }
            public List<PollutantInfo> Pollutants { get; set; } = new List<PollutantInfo>();
        }
    }
}
