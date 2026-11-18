using System.Collections.Concurrent;
using AqieHistoricaldataBackend.Atomfeed.Services;
using FluentAssertions;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionFilterLast7DaysTest
    {
        #region Apply - null / empty source

        [Fact]
        public void Apply_ReturnsEmptyList_WhenSourceIsNull()
        {
            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(null!, x => x.ReportDate);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void Apply_ReturnsEmptyList_WhenSourceIsEmpty()
        {
            // Arrange
            var source = new List<FinalData>();

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Apply_ReturnsEmptyList_WhenAllDatesAreUnparsable()
        {
            // Arrange
            var source = new List<FinalData>
            {
                new() { ReportDate = "not-a-date" },
                new() { ReportDate = null },
                new() { ReportDate = "" }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region Apply - filtering logic

        [Fact]
        public void Apply_FiltersOutItemsWithUnparsableDates_ButKeepsValidOnes()
        {
            // Arrange
            var yesterday = DateTime.Today.AddDays(-1);
            var source = new List<FinalData>
            {
                new() { ReportDate = yesterday.ToString("yyyy-MM-dd") },
                new() { ReportDate = "invalid-date" },
                new() { ReportDate = null }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate);

            // Assert
            result.Should().HaveCount(1);
            result[0].ReportDate.Should().Be(yesterday.ToString("yyyy-MM-dd"));
        }

        [Fact]
        public void Apply_On18Nov2026_ReturnsOnly11To17Nov2026_ExcludingCurrentDay()
        {
            // Arrange
            var today = new DateTime(2026, 11, 18);
            var source = new List<FinalData>
            {
                new() { StartTime = "2026-11-10T23:00" }, // excluded - before window
                new() { StartTime = "2026-11-11T00:00" }, // included - first hour of window
                new() { StartTime = "2026-11-14T12:00" }, // included
                new() { StartTime = "2026-11-17T23:00" }, // included - last hour of window
                new() { StartTime = "2026-11-18T00:00" }, // excluded - current day
                new() { StartTime = "2026-11-18T09:00" }  // excluded - current day
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.StartTime, today);

            // Assert
            result.Select(x => x.StartTime).Should().BeEquivalentTo(
                "2026-11-11T00:00", "2026-11-14T12:00", "2026-11-17T23:00");
        }

        [Fact]
        public void Apply_ReturnsOnlyItemsWithin7DaysBeforeReferenceDate()
        {
            // Arrange
            var today = new DateTime(2026, 9, 8);
            var source = new List<FinalData>
            {
                new() { ReportDate = today.ToString("yyyy-MM-dd") },                 // today - excluded
                new() { ReportDate = today.AddDays(1).ToString("yyyy-MM-dd") },      // future - excluded
                new() { ReportDate = today.AddDays(-1).ToString("yyyy-MM-dd") },     // yesterday - included (max)
                new() { ReportDate = today.AddDays(-7).ToString("yyyy-MM-dd") },     // day -7 - included (boundary, min)
                new() { ReportDate = today.AddDays(-8).ToString("yyyy-MM-dd") },     // day -8 - excluded
                new() { ReportDate = today.AddDays(-100).ToString("yyyy-MM-dd") }    // far outside - excluded
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate, today);

            // Assert
            result.Select(x => x.ReportDate).Should().BeEquivalentTo(
                today.AddDays(-1).ToString("yyyy-MM-dd"),
                today.AddDays(-7).ToString("yyyy-MM-dd"));
        }

        [Fact]
        public void Apply_IgnoresTimeComponent_WhenComparingDates()
        {
            // Arrange
            var today = new DateTime(2026, 9, 9);
            var source = new List<FinalData>
            {
                new() { ReportDate = "2026-09-08T23:00:00" }, // last hourly reading of window
                new() { ReportDate = "2026-09-08T23:59:59" }, // after 23:00 cutoff - excluded
                new() { ReportDate = "2026-09-02T00:00:01" }, // first day of window
                new() { ReportDate = "2026-09-01T23:59:59" }  // just outside window
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate, today.AddHours(15));

            // Assert
            result.Select(x => x.ReportDate).Should().BeEquivalentTo(
                "2026-09-08T23:00:00", "2026-09-02T00:00:01");
        }

        [Fact]
        public void Apply_ReturnsEmpty_WhenAllDatesAreOutsideWindow()
        {
            // Arrange
            var source = new List<FinalData>
            {
                new() { ReportDate = "2026-01-01" }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate, new DateTime(2026, 11, 18));

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Apply_HandlesYearBoundary()
        {
            // Arrange
            var today = new DateTime(2027, 1, 3);
            var source = new List<FinalData>
            {
                new() { ReportDate = "2026-12-26" }, // excluded
                new() { ReportDate = "2026-12-27" }, // included
                new() { ReportDate = "2027-01-02" }, // included
                new() { ReportDate = "2027-01-03" }  // excluded - current day
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate, today);

            // Assert
            result.Select(x => x.ReportDate).Should().BeEquivalentTo("2026-12-27", "2027-01-02");
        }

        [Fact]
        public void Apply_UsesTimeComponent_WithInclusiveBoundariesAt00And23()
        {
            // Arrange
            var today = new DateTime(2026, 11, 18);
            var source = new List<FinalData>
            {
                new() { StartTime = "2026-11-10T23:59:59" }, // excluded - before 11/11 00:00
                new() { StartTime = "2026-11-11T00:00:00" }, // included - minDate boundary
                new() { StartTime = "2026-11-17T23:00:00" }, // included - maxDate boundary
                new() { StartTime = "2026-11-17T23:00:01" }, // excluded - after 17/11 23:00
                new() { StartTime = "2026-11-17T23:59:59" }  // excluded - after 17/11 23:00
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.StartTime, today.AddHours(11));

            // Assert
            result.Select(x => x.StartTime).Should().BeEquivalentTo(
                "2026-11-11T00:00:00", "2026-11-17T23:00:00");
        }

        [Fact]
        public void Apply_ReturnsEmpty_WhenOnlyCurrentDayDataExists()
        {
            // Arrange
            var source = new List<FinalData>
            {
                new() { StartTime = "2026-11-18T00:00" },
                new() { StartTime = "2026-11-18T10:00" }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.StartTime, new DateTime(2026, 11, 18));

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region ByStartTime

        [Fact]
        public void ByStartTime_ReturnsEmptyList_WhenBagIsEmpty()
        {
            // Arrange
            var bag = new ConcurrentBag<FinalData>();

            // Act
            var result = AtomDataSelectionFilterLast7Days.ByStartTime(bag);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ByStartTime_FiltersByStartTimeProperty()
        {
            // Arrange
            var today = new DateTime(2026, 9, 8);
            var bag = new ConcurrentBag<FinalData>
            {
                new() { StartTime = today.ToString("yyyy-MM-dd") },
                new() { StartTime = today.AddDays(-1).ToString("yyyy-MM-dd") },
                new() { StartTime = today.AddDays(-10).ToString("yyyy-MM-dd") },
                new() { StartTime = "invalid" }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.ByStartTime(bag, today);

            // Assert
            result.Should().HaveCount(1);
            result[0].StartTime.Should().Be(today.AddDays(-1).ToString("yyyy-MM-dd"));
        }

        #endregion

        #region ByReportDate

        [Fact]
        public void ByReportDate_ReturnsEmptyList_WhenSourceIsEmpty()
        {
            // Arrange
            var source = new List<FinalData>();

            // Act
            var result = AtomDataSelectionFilterLast7Days.ByReportDate(source);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ByReportDate_FiltersByReportDateProperty()
        {
            // Arrange
            var today = new DateTime(2026, 9, 8);
            var source = new List<FinalData>
            {
                new() { ReportDate = today.ToString("yyyy-MM-dd") },
                new() { ReportDate = today.AddDays(-1).ToString("yyyy-MM-dd") },
                new() { ReportDate = today.AddDays(-7).ToString("yyyy-MM-dd") },
                new() { ReportDate = today.AddDays(-8).ToString("yyyy-MM-dd") }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.ByReportDate(source, today);

            // Assert
            result.Should().HaveCount(2);
        }

        #endregion
    }
}