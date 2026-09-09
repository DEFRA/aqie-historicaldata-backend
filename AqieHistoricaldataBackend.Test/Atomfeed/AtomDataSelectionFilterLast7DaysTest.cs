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
            var today = DateTime.Today;
            var source = new List<FinalData>
            {
                new() { ReportDate = today.ToString("yyyy-MM-dd") },
                new() { ReportDate = "invalid-date" },
                new() { ReportDate = null }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate);

            // Assert
            result.Should().HaveCount(1);
            result[0].ReportDate.Should().Be(today.ToString("yyyy-MM-dd"));
        }

        [Fact]
        public void Apply_ReturnsOnlyItemsWithinLast7DaysWindow_AnchoredOnMaxDate()
        {
            // Arrange
            var maxDate = new DateTime(2026, 9, 8);
            var source = new List<FinalData>
            {
                new() { ReportDate = maxDate.ToString("yyyy-MM-dd") },                 // day 0 - included (max)
                new() { ReportDate = maxDate.AddDays(-1).ToString("yyyy-MM-dd") },     // day -1 - included
                new() { ReportDate = maxDate.AddDays(-6).ToString("yyyy-MM-dd") },     // day -6 - included (boundary, min)
                new() { ReportDate = maxDate.AddDays(-7).ToString("yyyy-MM-dd") },     // day -7 - excluded (just outside window)
                new() { ReportDate = maxDate.AddDays(-100).ToString("yyyy-MM-dd") }    // far outside - excluded
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate);

            // Assert
            result.Should().HaveCount(3);
            result.Select(x => x.ReportDate).Should().Contain(new[]
            {
                maxDate.ToString("yyyy-MM-dd"),
                maxDate.AddDays(-1).ToString("yyyy-MM-dd"),
                maxDate.AddDays(-6).ToString("yyyy-MM-dd")
            });
        }

        [Fact]
        public void Apply_IgnoresTimeComponent_WhenComparingDates()
        {
            // Arrange
            var source = new List<FinalData>
            {
                new() { ReportDate = "2026-09-08T23:59:59" },
                new() { ReportDate = "2026-09-02T00:00:01" } // still within 6-day window by date
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate);

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public void Apply_ReturnsSingleItem_WhenOnlyOneValidDateExists()
        {
            // Arrange
            var source = new List<FinalData>
            {
                new() { ReportDate = "2026-01-01" }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.Apply(source, x => x.ReportDate);

            // Assert
            result.Should().HaveCount(1);
            result[0].ReportDate.Should().Be("2026-01-01");
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
            var maxDate = new DateTime(2026, 9, 8);
            var bag = new ConcurrentBag<FinalData>
            {
                new() { StartTime = maxDate.ToString("yyyy-MM-dd") },
                new() { StartTime = maxDate.AddDays(-10).ToString("yyyy-MM-dd") },
                new() { StartTime = "invalid" }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.ByStartTime(bag);

            // Assert
            result.Should().HaveCount(1);
            result[0].StartTime.Should().Be(maxDate.ToString("yyyy-MM-dd"));
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
            var maxDate = new DateTime(2026, 9, 8);
            var source = new List<FinalData>
            {
                new() { ReportDate = maxDate.ToString("yyyy-MM-dd") },
                new() { ReportDate = maxDate.AddDays(-6).ToString("yyyy-MM-dd") },
                new() { ReportDate = maxDate.AddDays(-7).ToString("yyyy-MM-dd") }
            };

            // Act
            var result = AtomDataSelectionFilterLast7Days.ByReportDate(source);

            // Assert
            result.Should().HaveCount(2);
        }

        #endregion
    }
}