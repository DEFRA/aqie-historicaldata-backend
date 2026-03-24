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
    public class AwsPreSignedUrLServiceTest
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCsv>> _mockLogger;
        private readonly Mock<IAmazonS3> _mockS3Client;
        private readonly AwsPreSignedUrLService _service;

        public AwsPreSignedUrLServiceTest()
        {
            _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCsv>>();
            _mockS3Client = new Mock<IAmazonS3>();
            _service = new AwsPreSignedUrLService(_mockLogger.Object, _mockS3Client.Object);
        }

        [Fact]
        public async Task GeneratePreSignedURL_ReturnsUrl_WhenSuccessful()
        {
            // Arrange
            string expectedUrl = "https://example.com/presigned-url";
            _mockS3Client.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                         .ReturnsAsync(expectedUrl);

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
            _mockS3Client.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                         .ThrowsAsync(s3Exception);

            // Act
            var result = await _service.GeneratePreSignedURL("bucket", "key", 60);

            // Assert
            Assert.Equal("error", result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("AmazonS3Exception Error:S3 error")),
                    It.IsAny<Exception>(),   
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GeneratePreSignedURL_ReturnsError_WhenGenericExceptionThrown()
        {
            // Arrange
            var ex = new Exception("Generic error");
            _mockS3Client.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                         .ThrowsAsync(ex);

            // Act
            var result = await _service.GeneratePreSignedURL("bucket", "key", 60);

            // Assert
            Assert.Equal("error", result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error in GeneratePreSignedURL Info message Generic error")),
                    It.IsAny<Exception>(),  
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error in GeneratePreSignedURL")),
                    It.IsAny<Exception>(),   
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once); 
        }

        [Fact]
        public async Task GeneratePreSignedURL_EndToEnd_SuccessAndErrorCases()
        {
            // Success
            string expectedUrl = "https://example.com/presigned-url";
            _mockS3Client.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                         .ReturnsAsync(expectedUrl);
            var resultSuccess = await _service.GeneratePreSignedURL("bucket", "key", 60);
            Assert.Equal(expectedUrl, resultSuccess);

            // AmazonS3Exception
            _mockS3Client.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                         .ThrowsAsync(new AmazonS3Exception("S3 error"));
            var resultS3Error = await _service.GeneratePreSignedURL("bucket", "key", 60);
            Assert.Equal("error", resultS3Error);

            // Generic Exception
            _mockS3Client.Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                         .ThrowsAsync(new Exception("Generic error"));
            var resultGenericError = await _service.GeneratePreSignedURL("bucket", "key", 60);
            Assert.Equal("error", resultGenericError);
        }
    }
}
