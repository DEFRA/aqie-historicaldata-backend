namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Aggregates job-status and notification services to reduce constructor parameter count.
    /// </summary>
    public class AtomJobNotificationServices(
        IAtomDataSelectionJobStatus jobStatus,
        IAtomDataSelectionEmailJobService emailJobService,
        IAtomDataSelectionPresignedUrlMail presignedUrlMail)
    {
        public IAtomDataSelectionJobStatus JobStatus { get; } = jobStatus;
        public IAtomDataSelectionEmailJobService EmailJobService { get; } = emailJobService;
        public IAtomDataSelectionPresignedUrlMail PresignedUrlMail { get; } = presignedUrlMail;
    }
}