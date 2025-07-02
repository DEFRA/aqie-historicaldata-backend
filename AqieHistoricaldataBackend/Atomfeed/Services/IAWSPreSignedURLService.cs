namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAWSPreSignedURLService
    {
        string GeneratePreSignedURL(string bucketName, string keyName, double duration);
    }
}
