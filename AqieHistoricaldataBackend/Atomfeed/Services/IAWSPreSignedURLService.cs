using System.Diagnostics.CodeAnalysis;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAwsPreSignedUrLService
    {
        /// <summary>
        /// Generates a pre-signed S3 URL for the given bucket and key.
        /// </summary>
        /// <param name="bucketName">The S3 bucket name.</param>
        /// <param name="s3Key">The S3 object key.</param>
        /// <param name="expiryInSeconds">URL expiry duration in seconds.</param>
        /// <returns>The pre-signed URL, or null if generation fails.</returns>
        [ExcludeFromCodeCoverage]
        Task<string?> GeneratePreSignedURL(string? bucketName, string s3Key, int expiryInSeconds);
    }
}
