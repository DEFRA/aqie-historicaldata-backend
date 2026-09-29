using System.Text;
using System.Text.Json;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AuthService : IAuthService
    {
        private const string FailureResult = "Failure";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IHttpClientFactory httpClientFactory, ILogger<AuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> GetTokenAsync(string email, string password)
        {
            var client = _httpClientFactory.CreateClient("RicardoNewAPI");

            var payload = new { email, password };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/login_check", content);

            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // Check for plain string root BEFORE attempting any property access
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return doc.RootElement.GetString();

            // Only access properties when root is an Object
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("token", out var token)) return token.GetString();
                if (doc.RootElement.TryGetProperty("access_token", out var accessToken)) return accessToken.GetString();
                if (doc.RootElement.TryGetProperty("jwt", out var jwt)) return jwt.GetString();
            }

            return null;
        }
        public async Task<string> GetRicardoToken()
        {
            var emailFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_KEY");
            var passwordFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_VALUE");

            if (string.IsNullOrEmpty(emailFromConfig) || string.IsNullOrEmpty(passwordFromConfig))
            {
                _logger.LogError("GetRicardoToken Auth failed - credentials not found in environment variables");
                return "Failure";
            }

            var token = await GetTokenAsync(emailFromConfig, passwordFromConfig);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("GetRicardoToken Auth failed - no token returned");
                return "Failure";
            }

            return token;
        }
    }
}