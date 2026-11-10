using AqieHistoricaldataBackend.Atomfeed.Services;
using FluentAssertions;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed.Services
{
    public class RegionCountryMapperTest
    {
        [Theory]
        [InlineData("1", "Northern Ireland")]
        [InlineData("3", "Scotland")]
        [InlineData("4", "Wales")]
        [InlineData("5", "Scotland")]
        [InlineData("6", "Scotland")]
        [InlineData("7", "England")]
        [InlineData("8", "England")]
        [InlineData("9", "Wales")]
        [InlineData("10", "England")]
        [InlineData("11", "England")]
        [InlineData("12", "England")]
        [InlineData("13", "Scotland")]
        [InlineData("14", "England")]
        [InlineData("15", "England")]
        [InlineData("16", "England")]
        [InlineData("17", "England")]
        [InlineData("18", "Other")]
        public void GetCountryByRegionId_ReturnsExpectedCountry(string regionId, string expected)
        {
            RegionCountryMapper.GetCountryByRegionId(regionId).Should().Be(expected);
        }

        [Theory]
        [InlineData(" 7 ", "England")]
        [InlineData("\t9\n", "Wales")]
        public void GetCountryByRegionId_TrimsWhitespace(string regionId, string expected)
        {
            RegionCountryMapper.GetCountryByRegionId(regionId).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("2")]
        [InlineData("99")]
        [InlineData("abc")]
        public void GetCountryByRegionId_ReturnsUnknown_ForInvalidInput(string? regionId)
        {
            RegionCountryMapper.GetCountryByRegionId(regionId)
                .Should().Be(RegionCountryMapper.UnknownCountry);
        }

        [Fact]
        public void UnknownCountry_HasExpectedValue()
        {
            RegionCountryMapper.UnknownCountry.Should().Be("Unknown");
        }

        [Fact]
        public void MapCountry_SetsCountry_AndReturnsSameInstance()
        {
            var site = new SiteInfo { RegionId = "15" };

            var result = site.MapCountry();

            result.Should().BeSameAs(site);
            result.Country.Should().Be("England");
        }

        [Fact]
        public void MapCountry_SetsUnknown_WhenRegionIdMissing()
        {
            var site = new SiteInfo { RegionId = null };

            site.MapCountry().Country.Should().Be(RegionCountryMapper.UnknownCountry);
        }

        [Fact]
        public void MapCountry_Throws_WhenSiteIsNull()
        {
            SiteInfo? site = null;

            Action act = () => site!.MapCountry();

            act.Should().Throw<ArgumentNullException>().WithParameterName("site");
        }

        [Fact]
        public void MapCountriesByRegion_MapsAllSites()
        {
            var sites = new List<SiteInfo>
            {
                new() { RegionId = "1" },
                new() { RegionId = "9" },
                new() { RegionId = "999" }
            };

            var result = sites.MapCountriesByRegion();

            result.Should().BeSameAs(sites);
            result.Select(s => s.Country).Should()
                .Equal("Northern Ireland", "Wales", RegionCountryMapper.UnknownCountry);
        }

        [Fact]
        public void MapCountriesByRegion_ReturnsEmptyList_WhenNull()
        {
            List<SiteInfo>? sites = null;

            var result = sites.MapCountriesByRegion();

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void MapCountriesByRegion_ReturnsSameEmptyList_WhenEmpty()
        {
            var sites = new List<SiteInfo>();

            var result = sites.MapCountriesByRegion();

            result.Should().BeSameAs(sites);
            result.Should().BeEmpty();
        }
    }
}