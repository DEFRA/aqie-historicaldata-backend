using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AqieHistoricaldataBackend.Tests.Services
{
    [Collection("AuthServiceEnvVars")]
    public class AuthServiceTest
    {
        private const string EmailVar = "RICARDO_API_KEY";
        private const string PasswordVar = "RICARDO_API_VALUE";

        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<ILogger<AuthService>> _loggerMock = new();

        private AuthService CreateService(HttpResponseMessage? response = null, Exception? exception = null)
        {
            var handler = new Mock<HttpMessageHandler>();
            var setup = handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

            if (exception != null)
                setup.ThrowsAsync(exception);
            else
                setup.ReturnsAsync(response!);

            var client = new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://test.com/")
            };

            _httpClientFactoryMock
                .Setup(x => x.CreateClient("RicardoNewAPI"))
                .Returns(client);

            return new AuthService(_httpClientFactoryMock.Object, _loggerMock.Object);
        }

        private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        private void VerifyLoggedError(string contains)
        {
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(contains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsNull_WhenResponseNotSuccess()
        {
            var service = CreateService(new HttpResponseMessage(HttpStatusCode.Unauthorized));

            var result = await service.GetTokenAsync("user@test.com", "pwd");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsValue_WhenRootIsPlainString()
        {
            var service = CreateService(Ok("\"plain-token\""));

            var result = await service.GetTokenAsync("user@test.com", "pwd");

            Assert.Equal("plain-token", result);
        }

        [Theory]
        [InlineData("{\"token\":\"tok-1\"}", "tok-1")]
        [InlineData("{\"access_token\":\"tok-2\"}", "tok-2")]
        [InlineData("{\"jwt\":\"tok-3\"}", "tok-3")]
        public async Task GetTokenAsync_ReturnsToken_ForSupportedProperties(string body, string expected)
        {
            var service = CreateService(Ok(body));

            var result = await service.GetTokenAsync("user@test.com", "pwd");

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsNull_WhenObjectHasNoKnownProperty()
        {
            var service = CreateService(Ok("{\"other\":\"value\"}"));

            var result = await service.GetTokenAsync("user@test.com", "pwd");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTokenAsync_ReturnsNull_WhenRootIsArray()
        {
            var service = CreateService(Ok("[1,2,3]"));

            var result = await service.GetTokenAsync("user@test.com", "pwd");

            Assert.Null(result);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("user@test.com", null)]
        [InlineData(null, "pwd")]
        [InlineData("user@test.com", "")]
        public async Task GetRicardoToken_ReturnsFailure_WhenCredentialsMissing(string? email, string? password)
        {
            using var _ = new EnvVarScope(email, password);
            var service = CreateService(Ok("{\"token\":\"tok\"}"));

            var result = await service.GetRicardoToken();

            Assert.Equal("Failure", result);
            VerifyLoggedError("credentials not found");
        }

        [Fact]
        public async Task GetRicardoToken_ReturnsFailure_WhenNoTokenReturned()
        {
            using var _ = new EnvVarScope("user@test.com", "pwd");
            var service = CreateService(new HttpResponseMessage(HttpStatusCode.BadRequest));

            var result = await service.GetRicardoToken();

            Assert.Equal("Failure", result);
            VerifyLoggedError("no token returned");
        }

        [Fact]
        public async Task GetRicardoToken_ReturnsToken_WhenSuccessful()
        {
            using var _ = new EnvVarScope("user@test.com", "pwd");
            var service = CreateService(Ok("{\"token\":\"tok-success\"}"));

            var result = await service.GetRicardoToken();

            Assert.Equal("tok-success", result);
        }

        private sealed class EnvVarScope : IDisposable
        {
            private readonly string? _originalEmail;
            private readonly string? _originalPassword;

            public EnvVarScope(string? email, string? password)
            {
                _originalEmail = Environment.GetEnvironmentVariable(EmailVar);
                _originalPassword = Environment.GetEnvironmentVariable(PasswordVar);
                Environment.SetEnvironmentVariable(EmailVar, email);
                Environment.SetEnvironmentVariable(PasswordVar, password);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(EmailVar, _originalEmail);
                Environment.SetEnvironmentVariable(PasswordVar, _originalPassword);
            }
        }
    }

    [CollectionDefinition("AuthServiceEnvVars", DisableParallelization = true)]
    public class AuthServiceEnvVarsCollection { }
}