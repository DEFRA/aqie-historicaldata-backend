using Amazon.S3.Model;
using Amazon.S3;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AWSPreSignedURLService(ILogger<HourlyAtomFeedExportCSV> logger) : IAWSPreSignedURLService
    {
        public async Task<string> GeneratePreSignedURL(string bucketName, string keyName, double duration)
        {
            try
            {
                var s3Client = new AmazonS3Client();
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = keyName,
                    Expires = DateTime.UtcNow.AddSeconds(duration)
                };

                string url = s3Client.GetPreSignedURL(request);
                return url;
            }
            catch (AmazonS3Exception ex)
            {
                logger.LogError("AmazonS3Exception Error:{Error}", ex.Message);
                return "error";
            }
            catch (Exception ex)
            {
                logger.LogError("Error in GeneratePreSignedURL Info message {Error}", ex.Message);
                logger.LogError("Error in GeneratePreSignedURL {Error}", ex.StackTrace);
                return "error";
            }
        }
    }
}
