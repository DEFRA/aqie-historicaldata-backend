using Amazon.S3.Model;
using Amazon.S3;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AwsPreSignedUrLService(ILogger<HourlyAtomFeedExportCsv> logger, IAmazonS3 s3Client) : IAwsPreSignedUrLService
    {
        public async Task<string?> GeneratePreSignedURL(string? bucketName, string s3Key, int expiryInSeconds)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = s3Key,
                    Expires = DateTime.UtcNow.AddSeconds(expiryInSeconds)
                };

                string url = await s3Client.GetPreSignedURLAsync(request);
                return url;
            }
            catch (AmazonS3Exception ex)
            {
                logger.LogError(ex, "AmazonS3Exception Error:{Message}", ex.Message);
                return "error";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in GeneratePreSignedURL Info message {Message}", ex.Message);
                return "error";
            }
        }
    }
}
