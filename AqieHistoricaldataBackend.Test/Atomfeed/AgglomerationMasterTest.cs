using AqieHistoricaldataBackend.Atomfeed.Models;
using FluentAssertions;
using Xunit;

namespace AqieHistoricaldataBackend.Test.Atomfeed.Services
{
    public class AgglomerationMasterTest
    {
        [Fact]
        public void Map_ShouldContainAllExpectedEntries()
        {
            AgglomerationMaster.Map.Should().HaveCount(28);
            AgglomerationMaster.Map["1"].Should().Be("Belfast Metropolitan Urban Area");
            AgglomerationMaster.Map["28"].Should().Be("Sheffield Urban Area");
        }

        [Fact]
        public void Map_ShouldBeCaseInsensitive_AndContainDuplicateLondonEntries()
        {
            AgglomerationMaster.Map.ContainsKey("9").Should().BeTrue();
            AgglomerationMaster.Map["9"].Should().Be(AgglomerationMaster.Map["10"]);
        }

        [Theory]
        [InlineData("1", "Belfast Metropolitan Urban Area")]
        [InlineData("3", "Greater Manchester Urban Area")]
        [InlineData("9", "Greater London Urban Area")]
        [InlineData("10", "Greater London Urban Area")]
        [InlineData("22", "The Potteries")]
        [InlineData("28", "Sheffield Urban Area")]
        public void Resolve_ShouldReturnRegion_WhenIdExists(string id, string expected)
        {
            AgglomerationMaster.Resolve(id).Should().Be(expected);
        }

        [Theory]
        [InlineData(" 7 ")]
        [InlineData("\t12\n")]
        public void Resolve_ShouldTrimInput(string id)
        {
            AgglomerationMaster.Resolve(id).Should().NotBe("Not found in provided mapping");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0")]
        [InlineData("29")]
        [InlineData("abc")]
        [InlineData("1.0")]
        public void Resolve_ShouldReturnNotFound_ForInvalidInput(string? id)
        {
            AgglomerationMaster.Resolve(id).Should().Be("Not found in provided mapping");
        }

        [Fact]
        public void Map_ShouldNotContainNullOrEmptyValues()
        {
            AgglomerationMaster.Map.Values.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v));
        }
    }
}