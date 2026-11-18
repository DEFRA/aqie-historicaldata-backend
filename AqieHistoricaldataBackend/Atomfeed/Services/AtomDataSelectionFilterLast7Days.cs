using System.Collections.Concurrent;
using System.Globalization;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Shared helper to filter <see cref="FinalData"/> collections down to the 7 full days
    /// preceding the current date (the current day is excluded).
    /// e.g. when today is 18 November 2026, only data from 11 November 2026 to 17 November 2026
    /// (inclusive) is returned.
    /// </summary>
    public static class AtomDataSelectionFilterLast7Days
    {
        /// <summary>
        /// Filters an <see cref="IEnumerable{FinalData}"/> (e.g. List, ConcurrentBag) to the
        /// 7 days before <paramref name="referenceDate"/> (defaults to <see cref="DateTime.Today"/>),
        /// based on the date extracted via <paramref name="dateSelector"/>. The reference date itself is excluded.
        /// </summary>
        public static List<FinalData> Apply(
            IEnumerable<FinalData> source,
            Func<FinalData, string?> dateSelector,
            DateTime? referenceDate = null)
        {
            if (source == null)
                return new List<FinalData>();

            var today = (referenceDate ?? DateTime.Today).Date;
            var minDate = today.AddDays(-7);   // inclusive window start, e.g. 11/11/2026 00:00:00
            var maxDate = today.AddHours(-1);  // inclusive last hourly reading, e.g. 17/11/2026 23:00:00

            return source
                .Select(item => new { Item = item, DateValue = TryParseDate(dateSelector(item)) })
                .Where(x => x.DateValue.HasValue
                            && x.DateValue.Value >= minDate
                            && x.DateValue.Value <= maxDate)
                .Select(x => x.Item)
                .ToList();
        }

        /// <summary>
        /// Convenience overload for hourly data, filtering by <see cref="FinalData.StartTime"/>.
        /// </summary>
        public static List<FinalData> ByStartTime(ConcurrentBag<FinalData> resultsBag, DateTime? referenceDate = null) =>
            Apply(resultsBag, x => x.StartTime, referenceDate);

        /// <summary>
        /// Convenience overload for daily/annual data, filtering by <see cref="FinalData.ReportDate"/>.
        /// </summary>
        public static List<FinalData> ByReportDate(IEnumerable<FinalData> finalList, DateTime? referenceDate = null) =>
            Apply(finalList, x => x.ReportDate, referenceDate);

        private static DateTime? TryParseDate(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : (DateTime?)null;
    }
}