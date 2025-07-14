using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Model;
using AqieHistoricaldataBackend.Atomfeed.Services;
using System;
using System.Threading.Tasks;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AWSPreSignedURLServiceTest
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _mockLogger;
        private readonly Mock<IAmazonS3> _mockS3Client;
        private readonly AWSPreSignedURLService _service;

        public AWSPreSignedURLServiceTest()
        {
            _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
            _mockS3Client = new Mock<IAmazonS3>();
            _service = new AWSPreSignedURLService(_mockLogger.Object, _mockS3Client.Object);
        }

        [Fact]
        public async Task GeneratePreSignedURL_ReturnsUrl_WhenSuccessful()
        {
            // Arrange
            string expectedUrl = "https://example.com/presigned-url";
            _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
                         .Returns(expectedUrl);

            // Act
            var result = await _service.GeneratePreSignedURL("bucket", "key", 60);

            // Assert
            Assert.Equal(expectedUrl, result);
            _mockLogger.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GeneratePreSignedURL_ReturnsError_WhenAmazonS3ExceptionThrown()
        {
            // Arrange
            var s3Exception = new AmazonS3Exception("S3 error");
            _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
                         .Throws(s3Exception);

            // Act
            var result = await _service.GeneratePreSignedURL("bucket", "key", 60);

            // Assert
            Assert.Equal("error", result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("AmazonS3Exception Error:S3 error")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GeneratePreSignedURL_ReturnsError_WhenGenericExceptionThrown()
        {
            // Arrange
            var ex = new Exception("Generic error");
            _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
                         .Throws(ex);

            // Act
            var result = await _service.GeneratePreSignedURL("bucket", "key", 60);

            // Assert
            Assert.Equal("error", result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error in GeneratePreSignedURL Info message Generic error")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error in GeneratePreSignedURL")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GeneratePreSignedURL_EndToEnd_SuccessAndErrorCases()
        {
            // Success
            string expectedUrl = "https://example.com/presigned-url";
            _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
                         .Returns(expectedUrl);
            var resultSuccess = await _service.GeneratePreSignedURL("bucket", "key", 60);
            Assert.Equal(expectedUrl, resultSuccess);

            // AmazonS3Exception
            _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
                         .Throws(new AmazonS3Exception("S3 error"));
            var resultS3Error = await _service.GeneratePreSignedURL("bucket", "key", 60);
            Assert.Equal("error", resultS3Error);

            // Generic Exception
            _mockS3Client.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
                         .Throws(new Exception("Generic error"));
            var resultGenericError = await _service.GeneratePreSignedURL("bucket", "key", 60);
            Assert.Equal("error", resultGenericError);
        }
    }
}
