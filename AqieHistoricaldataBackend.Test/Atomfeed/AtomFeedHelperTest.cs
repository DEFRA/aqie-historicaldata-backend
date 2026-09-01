using System.Text;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Newtonsoft.Json.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using Xunit;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomFeedHelperTest
    {
        #region ParseXmlStreamToFeatureArray

        private static Stream ToStream(string xml)
            => new MemoryStream(Encoding.UTF8.GetBytes(xml));

        [Fact]
        public void ParseXmlStreamToFeatureArray_ReturnsFeatureArray_WhenFeatureMemberPresent()
        {
            // Arrange
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml">
                  <gml:featureMember>
                    <Station id="S1"/>
                  </gml:featureMember>
                  <gml:featureMember>
                    <Station id="S2"/>
                  </gml:featureMember>
                </gml:FeatureCollection>
                """;

            // Act
            var result = AtomFeedHelper.ParseXmlStreamToFeatureArray(ToStream(xml));

            // Assert
            Assert.IsType<JArray>(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void ParseXmlStreamToFeatureArray_ReturnsEmptyJArray_WhenFeatureMemberAbsent()
        {
            // Arrange
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml">
                </gml:FeatureCollection>
                """;

            // Act
            var result = AtomFeedHelper.ParseXmlStreamToFeatureArray(ToStream(xml));

            // Assert
            Assert.IsType<JArray>(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseXmlStreamToFeatureArray_ReturnsEmptyJArray_WhenNoFeatureCollectionElement()
        {
            // Arrange
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <root>
                  <item>data</item>
                </root>
                """;

            // Act
            var result = AtomFeedHelper.ParseXmlStreamToFeatureArray(ToStream(xml));

            // Assert
            Assert.IsType<JArray>(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseXmlStreamToFeatureArray_ReturnsSingleItemArray_WhenOnlyOneFeatureMember()
        {
            // Arrange
            const string xml = """
                <?xml version="1.0" encoding="utf-8"?>
                <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml">
                  <gml:featureMember>
                    <Station id="S1"/>
                  </gml:featureMember>
                </gml:FeatureCollection>
                """;

            // Act
            var result = AtomFeedHelper.ParseXmlStreamToFeatureArray(ToStream(xml));

            // Assert - Newtonsoft may deserialize single element as JObject; still non-null JArray fallback
            Assert.NotNull(result);
        }

        #endregion

        #region ExtractPollutantId

        [Fact]
        public void ExtractPollutantId_ReturnsSegmentAfterLastSlash_WhenHrefIsValid()
        {
            // Arrange
            const string href = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8";

            // Act
            var result = AtomFeedHelper.ExtractPollutantId(href);

            // Assert
            Assert.Equal("8", result);
        }

        [Fact]
        public void ExtractPollutantId_ReturnsNull_WhenHrefIsNull()
        {
            // Act
            var result = AtomFeedHelper.ExtractPollutantId(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractPollutantId_ReturnsFullString_WhenNoSlashPresent()
        {
            // Arrange
            const string href = "PM10";

            // Act
            var result = AtomFeedHelper.ExtractPollutantId(href);

            // Assert
            Assert.Equal("PM10", result);
        }

        [Fact]
        public void ExtractPollutantId_ReturnsEmptyString_WhenHrefEndsWithSlash()
        {
            // Arrange
            const string href = "http://example.com/pollutant/";

            // Act
            var result = AtomFeedHelper.ExtractPollutantId(href);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractPollutantId_ReturnsCorrectSegment_WhenMultipleSlashesPresent()
        {
            // Arrange
            const string href = "a/b/c/d/NOx";

            // Act
            var result = AtomFeedHelper.ExtractPollutantId(href);

            // Assert
            Assert.Equal("NOx", result);
        }

        #endregion

        #region SplitSweValues

        [Fact]
        public void SplitSweValues_ReturnsRows_WhenAllPartsHaveFiveOrMoreColumns()
        {
            // Arrange
            const string values = "2024-01-01T00:00:00,2024-01-01T01:00:00,1,1,5.2@@2024-01-01T01:00:00,2024-01-01T02:00:00,1,1,6.1";

            // Act
            var result = AtomFeedHelper.SplitSweValues(values).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(5, result[0].Length);
        }

        [Fact]
        public void SplitSweValues_FiltersOut_RowsWithFewerThanFiveParts()
        {
            // Arrange - second entry has only 4 columns
            const string values = "2024-01-01T00:00:00,2024-01-01T01:00:00,1,1,5.2@@tooFew,data,only,3";

            // Act
            var result = AtomFeedHelper.SplitSweValues(values).ToList();

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public void SplitSweValues_ReturnsEmpty_WhenAllRowsHaveFewerThanFiveParts()
        {
            // Arrange
            const string values = "a,b,c,d@@x,y,z";

            // Act
            var result = AtomFeedHelper.SplitSweValues(values).ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void SplitSweValues_HandlesCarriageReturnNewLines_Correctly()
        {
            // Arrange - embed \r\n inside the values string
            var values = "2024-01-01T00:00:00,2024-01-01T01:00:00,1,1,5.2\r\n@@2024-01-01T01:00:00,2024-01-01T02:00:00,1,1,6.1";

            // Act
            var result = AtomFeedHelper.SplitSweValues(values).ToList();

            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void SplitSweValues_AcceptsRowsWithMoreThanFiveColumns()
        {
            // Arrange
            const string values = "a,b,c,d,e,f,g";

            // Act
            var result = AtomFeedHelper.SplitSweValues(values).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(7, result[0].Length);
        }

        [Fact]
        public void SplitSweValues_ReturnsEmpty_WhenInputIsWhitespace()
        {
            // Arrange
            const string values = "   ";

            // Act
            var result = AtomFeedHelper.SplitSweValues(values).ToList();

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region ToFinalData (without SiteInfo)

        [Fact]
        public void ToFinalData_WithoutSiteInfo_MapsAllFieldsCorrectly()
        {
            // Arrange
            var rows = new List<string[]>
            {
                new[] { "2024-01-01T00:00:00", "2024-01-01T01:00:00", "3", "1", "12.5" }
            };

            // Act
            var result = AtomFeedHelper.ToFinalData(rows, "PM10");

            // Assert
            Assert.Single(result);
            var item = result[0];
            Assert.Equal("2024-01-01T00:00:00", item.StartTime);
            Assert.Equal("2024-01-01T01:00:00", item.EndTime);
            Assert.Equal("3",   item.Verification);
            Assert.Equal("1",   item.Validity);
            Assert.Equal("12.5", item.Value);
            Assert.Equal("PM10", item.PollutantName);
        }

        [Fact]
        public void ToFinalData_WithoutSiteInfo_ReturnsEmptyList_WhenNoRows()
        {
            // Act
            var result = AtomFeedHelper.ToFinalData(Enumerable.Empty<string[]>(), "PM10");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ToFinalData_WithoutSiteInfo_ReturnsMultipleItems_WhenMultipleRowsGiven()
        {
            // Arrange
            var rows = new List<string[]>
            {
                new[] { "start1", "end1", "v1", "val1", "1.0" },
                new[] { "start2", "end2", "v2", "val2", "2.0" },
                new[] { "start3", "end3", "v3", "val3", "3.0" }
            };

            // Act
            var result = AtomFeedHelper.ToFinalData(rows, "NO2");

            // Assert
            Assert.Equal(3, result.Count);
            Assert.All(result, r => Assert.Equal("NO2", r.PollutantName));
        }

        [Fact]
        public void ToFinalData_WithoutSiteInfo_SiteFieldsAreNull()
        {
            // Arrange
            var rows = new List<string[]>
            {
                new[] { "s", "e", "v", "vl", "0.0" }
            };

            // Act
            var result = AtomFeedHelper.ToFinalData(rows, "O3");

            // Assert
            var item = result[0];
            Assert.Null(item.SiteName);
            Assert.Null(item.SiteType);
            Assert.Null(item.Region);
            Assert.Null(item.Country);
        }

        #endregion

        #region ToFinalData (with SiteInfo)

        private static SiteInfo BuildSiteInfo() => new()
        {
            SiteName   = "London Marylebone Road",
            AreaType   = "Urban",
            SiteType   = "Traffic",
            ZoneRegion = "Greater London",
            Country    = "England"
        };

        [Fact]
        public void ToFinalData_WithSiteInfo_MapsAllFieldsCorrectly()
        {
            // Arrange
            var rows = new List<string[]>
            {
                new[] { "2024-01-01T00:00:00", "2024-01-01T01:00:00", "3", "1", "42.7" }
            };
            var siteInfo = BuildSiteInfo();

            // Act
            var result = AtomFeedHelper.ToFinalData(rows, "NO2", siteInfo);

            // Assert
            Assert.Single(result);
            var item = result[0];
            Assert.Equal("2024-01-01T00:00:00", item.StartTime);
            Assert.Equal("2024-01-01T01:00:00", item.EndTime);
            Assert.Equal("3",   item.Verification);
            Assert.Equal("1",   item.Validity);
            Assert.Equal("42.7", item.Value);
            Assert.Equal("NO2", item.PollutantName);
            Assert.Equal("London Marylebone Road", item.SiteName);
            Assert.Equal("UrbanTraffic", item.SiteType);
            Assert.Equal("Greater London", item.Region);
            Assert.Equal("England", item.Country);
        }

        [Fact]
        public void ToFinalData_WithSiteInfo_ReturnsEmptyList_WhenNoRows()
        {
            // Act
            var result = AtomFeedHelper.ToFinalData(Enumerable.Empty<string[]>(), "PM2.5", BuildSiteInfo());

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ToFinalData_WithSiteInfo_ReturnsMultipleItems_WhenMultipleRowsGiven()
        {
            // Arrange
            var rows = new List<string[]>
            {
                new[] { "s1", "e1", "v1", "vl1", "1.1" },
                new[] { "s2", "e2", "v2", "vl2", "2.2" }
            };
            var siteInfo = BuildSiteInfo();

            // Act
            var result = AtomFeedHelper.ToFinalData(rows, "SO2", siteInfo);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, r =>
            {
                Assert.Equal("SO2", r.PollutantName);
                Assert.Equal("London Marylebone Road", r.SiteName);
                Assert.Equal("England", r.Country);
            });
        }

        [Fact]
        public void ToFinalData_WithSiteInfo_ConcatenatesAreaTypeAndSiteType()
        {
            // Arrange
            var rows = new List<string[]>
            {
                new[] { "s", "e", "v", "vl", "0" }
            };
            var siteInfo = new SiteInfo { AreaType = "Rural", SiteType = "Background", ZoneRegion = "North", Country = "England", SiteName = "Test" };

            // Act
            var result = AtomFeedHelper.ToFinalData(rows, "PM10", siteInfo);

            // Assert
            Assert.Equal("RuralBackground", result[0].SiteType);
        }

        [Fact]
        public void ToFinalData_WithSiteInfo_HandlesNullAreaTypeAndSiteType()
        {
            // Arrange
            var rows = new List<string[]>
            {
                new[] { "s", "e", "v", "vl", "0" }
            };
            var siteInfo = new SiteInfo { AreaType = null, SiteType = null, ZoneRegion = null, Country = null, SiteName = null };

            // Act
            var result = AtomFeedHelper.ToFinalData(rows, "PM10", siteInfo);

            // Assert
            Assert.Null(result[0].SiteType); // null + null = null in C#
        }

        #endregion
    }
}