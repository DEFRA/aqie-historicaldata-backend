using AqieHistoricaldataBackend.Atomfeed.Models;
using Xunit;

namespace AqieHistoricaldataBackend.Tests.Models
{
    public class AgglomerationMasterTest
    {
        [Theory]
        [InlineData("1", "Northern Ireland")]
        [InlineData("2", "South East")]
        [InlineData("3", "North West & Merseyside")]
        [InlineData("4", "East Midlands")]
        [InlineData("5", "South West")]
        [InlineData("6", "Scotland")]
        [InlineData("7", "North East")]
        [InlineData("8", "North East")]
        [InlineData("9", "Greater London")]
        [InlineData("10", "Eastern")]
        [InlineData("11", "Wales")]
        [InlineData("12", "Wales")]
        [InlineData("13", "South West")]
        [InlineData("14", "South East")]
        [InlineData("15", "South East")]
        [InlineData("16", "North West & Merseyside")]
        [InlineData("17", "North West & Merseyside")]
        [InlineData("18", "North West & Merseyside")]
        [InlineData("19", "North West & Merseyside")]
        [InlineData("20", "East Midlands")]
        [InlineData("21", "Scotland")]
        [InlineData("22", "West Midlands")]
        [InlineData("23", "West Midlands")]
        [InlineData("24", "South East")]
        [InlineData("25", "West Midlands")]
        [InlineData("26", "Yorkshire & Humberside")]
        [InlineData("27", "Yorkshire & Humberside")]
        [InlineData("28", "Yorkshire & Humberside")]
        [InlineData(" 28 ", "Yorkshire & Humberside")]
        public void Resolve_ReturnsMappedRegion(string id, string expected) =>
            Assert.Equal(expected, AgglomerationMaster.Resolve(id));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("999")]
        public void Resolve_ReturnsFallback_ForMissingOrUnknownId(string? id) =>
            Assert.Equal("Not found in provided mapping", AgglomerationMaster.Resolve(id));

        [Fact]
        public void Map_ContainsAllTwentyEightAgglomerations() =>
            Assert.Equal(28, AgglomerationMaster.Map.Count);
    }
}