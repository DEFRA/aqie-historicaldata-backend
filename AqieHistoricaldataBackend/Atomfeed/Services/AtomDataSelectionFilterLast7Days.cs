using System.Collections.Concurrent;
using System.Globalization;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Shared helper to filter <see cref="FinalData"/> collections down to the latest 7 days
    /// of data, anchored on the most recent date found within the data itself.
    /// </summary>
    public static class AtomDataSelectionFilterLast7Days
    {
        /// <summary>
        /// Filters an <see cref="IEnumerable{FinalData}"/> (e.g. List, ConcurrentBag) to the
        /// latest 7 days based on the date extracted via <paramref name="dateSelector"/>.
        /// </summary>
        public static List<FinalData> Apply(
            IEnumerable<FinalData> source,
            Func<FinalData, string?> dateSelector)
        {
            if (source == null)
                return new List<FinalData>();

            var parsed = source
                .Select(item => new { Item = item, DateValue = TryParseDate(dateSelector(item)) })
                .Where(x => x.DateValue.HasValue)
                .ToList();

            if (parsed.Count == 0)
                return new List<FinalData>();

            var maxDate = parsed.Max(x => x.DateValue!.Value.Date);
            var minDate = maxDate.AddDays(-6); // inclusive 7-day window

            return parsed
                .Where(x => x.DateValue!.Value.Date >= minDate && x.DateValue.Value.Date <= maxDate)
                .Select(x => x.Item)
                .ToList();
        }

        /// <summary>
        /// Convenience overload for hourly data, filtering by <see cref="FinalData.StartTime"/>.
        /// </summary>
        public static List<FinalData> ByStartTime(ConcurrentBag<FinalData> resultsBag) =>
            Apply(resultsBag, x => x.StartTime);

        /// <summary>
        /// Convenience overload for daily/annual data, filtering by <see cref="FinalData.ReportDate"/>.
        /// </summary>
        public static List<FinalData> ByReportDate(IEnumerable<FinalData> finalList) =>
            Apply(finalList, x => x.ReportDate);

        private static DateTime? TryParseDate(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : (DateTime?)null;
    }
}