using System;
using System.Collections.Generic;
using AqieHistoricaldataBackend.Atomfeed.Services;
using FluentAssertions;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed.Services
{
    public class AgglomerationCountryMapperTest
    {
        [Theory]
        [InlineData("1", "Northern Ireland")]
        [InlineData("2", "England")]
        [InlineData("3", "England")]
        [InlineData("4", "England")]
        [InlineData("5", "England")]
        [InlineData("6", "Scotland")]
        [InlineData("7", "England")]
        [InlineData("8", "England")]
        [InlineData("9", "England")]
        [InlineData("10", "England")]
        [InlineData("11", "Wales")]
        [InlineData("12", "Wales")]
        [InlineData("13", "England")]
        [InlineData("14", "England")]
        [InlineData("15", "England")]
        [InlineData("16", "England")]
        [InlineData("17", "England")]
        [InlineData("18", "England")]
        [InlineData("19", "England")]
        [InlineData("20", "England")]
        [InlineData("21", "Scotland")]
        [InlineData("22", "England")]
        [InlineData("23", "England")]
        [InlineData("24", "England")]
        [InlineData("25", "England")]
        [InlineData("26", "England")]
        [InlineData("27", "England")]
        [InlineData("28", "England")]
        public void GetCountryByAgglomerationId_ReturnsMappedCountry(string id, string expected)
        {
            AgglomerationCountryMapper.GetCountryByAgglomerationId(id).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetCountryByAgglomerationId_NullOrWhitespace_ReturnsUnknown(string? id)
        {
            AgglomerationCountryMapper.GetCountryByAgglomerationId(id)
                .Should().Be(AgglomerationCountryMapper.UnknownCountry);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("29")]
        [InlineData("abc")]
        public void GetCountryByAgglomerationId_UnmappedId_ReturnsUnknown(string id)
        {
            AgglomerationCountryMapper.GetCountryByAgglomerationId(id)
                .Should().Be(AgglomerationCountryMapper.UnknownCountry);
        }

        [Fact]
        public void GetCountryByAgglomerationId_TrimsWhitespace()
        {
            AgglomerationCountryMapper.GetCountryByAgglomerationId("  6  ").Should().Be("Scotland");
        }

        [Fact]
        public void UnknownCountry_HasExpectedValue()
        {
            AgglomerationCountryMapper.UnknownCountry.Should().Be("Unknown");
        }

        [Fact]
        public void MapCountryByAgglomeration_SetsCountryAndReturnsSameInstance()
        {
            var site = new SiteInfo { AgglomerationID = "11" };

            var result = site.MapCountryByAgglomeration();

            result.Should().BeSameAs(site);
            result.Country.Should().Be("Wales");
        }

        [Fact]
        public void MapCountryByAgglomeration_UnknownId_SetsUnknown()
        {
            var site = new SiteInfo { AgglomerationID = "999" };

            site.MapCountryByAgglomeration().Country
                .Should().Be(AgglomerationCountryMapper.UnknownCountry);
        }

        [Fact]
        public void MapCountryByAgglomeration_NullSite_Throws()
        {
            SiteInfo? site = null;

            Action act = () => site!.MapCountryByAgglomeration();

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void MapCountriesByAgglomeration_NullList_ReturnsEmptyList()
        {
            List<SiteInfo>? sites = null;

            var result = sites.MapCountriesByAgglomeration();

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void MapCountriesByAgglomeration_EmptyList_ReturnsSameEmptyInstance()
        {
            var sites = new List<SiteInfo>();

            var result = sites.MapCountriesByAgglomeration();

            result.Should().BeSameAs(sites);
            result.Should().BeEmpty();
        }

        [Fact]
        public void MapCountriesByAgglomeration_PopulatedList_MapsEverySite()
        {
            var sites = new List<SiteInfo>
            {
                new SiteInfo { AgglomerationID = "1" },
                new SiteInfo { AgglomerationID = "6" },
                new SiteInfo { AgglomerationID = "12" },
                new SiteInfo { AgglomerationID = "   " },
                new SiteInfo { AgglomerationID = null }
            };

            var result = sites.MapCountriesByAgglomeration();

            result.Should().BeSameAs(sites);
            result[0].Country.Should().Be("Northern Ireland");
            result[1].Country.Should().Be("Scotland");
            result[2].Country.Should().Be("Wales");
            result[3].Country.Should().Be(AgglomerationCountryMapper.UnknownCountry);
            result[4].Country.Should().Be(AgglomerationCountryMapper.UnknownCountry);
        }
    }
}