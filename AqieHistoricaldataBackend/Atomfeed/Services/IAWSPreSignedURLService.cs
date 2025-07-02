namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAWSPreSignedURLService
    {
        Task<string> GeneratePreSignedURL(string bucketName, string keyName, double duration);
    }
}
