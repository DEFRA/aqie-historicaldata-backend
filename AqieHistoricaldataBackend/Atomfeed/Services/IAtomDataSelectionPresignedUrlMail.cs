namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionPresignedUrlMail
    {
        Task<string> GetPresignedUrlMail(string jobId);
    }
}
