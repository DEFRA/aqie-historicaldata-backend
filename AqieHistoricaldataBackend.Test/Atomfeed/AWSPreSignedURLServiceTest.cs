using Xunit;
using Moq;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using System;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    //public class AWSPreSignedURLServiceTests
    //{
    //    private readonly Mock<IAmazonS3> _mockS3Client = new();
    //    private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _mockLogger = new();

    //    [Fact]
    //    public void GeneratePreSignedURL_ReturnsUrl_WhenSuccessful()
    //    {
    //        // Arrange
    //        var expectedUrl = "https://example.com/presigned";
    //        _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
    //                     .Returns(expectedUrl);

    //        var service = new AWSPreSignedURLService(_mockLogger.Object, _mockS3Client.Object);

    //        // Act
    //        var result = service.GeneratePreSignedURL("bucket", "key", 60);

    //        // Assert
    //        Assert.Equal(expectedUrl, result);
    //    }

    //    [Fact]
    //    public void GeneratePreSignedURL_ReturnsError_WhenAmazonS3ExceptionThrown()
    //    {
    //        // Arrange
    //        _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
    //                     .Throws(new AmazonS3Exception("S3 error"));

    //        var service = new AWSPreSignedURLService(_mockLogger.Object, _mockS3Client.Object);

    //        // Act
    //        var result = service.GeneratePreSignedURL("bucket", "key", 60);

    //        // Assert
    //        Assert.Equal("error", result);
    //        _mockLogger.Verify(
    //                 x => x.Log(
    //                 LogLevel.Error,
    //                 It.IsAny<EventId>(),
    //                 It.Is<It.IsAnyType>((v, t) => true),
    //                 It.IsAny<Exception>(),
    //                 It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    //                 Times.AtLeastOnce);
    //    }

    //    [Fact]
    //    public void GeneratePreSignedURL_ReturnsError_WhenGenericExceptionThrown()
    //    {
    //        // Arrange
    //        _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
    //                     .Throws(new Exception("Generic error"));

    //        var service = new AWSPreSignedURLService(_mockLogger.Object, _mockS3Client.Object);

    //        // Act
    //        var result = service.GeneratePreSignedURL("bucket", "key", 60);

    //        // Assert
    //        Assert.Equal("error", result);
    //        _mockLogger.Verify(
    //                         x => x.Log(
    //                         LogLevel.Error,
    //                         It.IsAny<EventId>(),
    //                         It.Is<It.IsAnyType>((v, t) => true),
    //                         It.IsAny<Exception>(),
    //                         It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    //                         Times.AtLeastOnce);
    //    }

    //    [Theory]
    //    [InlineData(null, "key", 60)]
    //    [InlineData("bucket", null, 60)]
    //    [InlineData("", "key", 60)]
    //    [InlineData("bucket", "", 60)]
    //    public void GeneratePreSignedURL_HandlesNullOrEmptyInputs(string bucket, string key, double duration)
    //    {
    //        // Arrange
    //        _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
    //                     .Returns("https://example.com");

    //        var service = new AWSPreSignedURLService(_mockLogger.Object, _mockS3Client.Object);

    //        // Act
    //        var result = service.GeneratePreSignedURL(bucket, key, duration);

    //        // Assert
    //        Assert.NotNull(result); // Still returns a URL or "error"
    //    }
    //}
}