namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionServices
    {
        IAtomDataSelectionStationBoundryService StationBoundry { get; }
        IAtomDataSelectionLocalAuthoritiesService LocalAuthorities { get; }
        IAtomDataSelectionHourlyFetchService HourlyFetch { get; }
    }

    public class AtomDataSelectionServices : IAtomDataSelectionServices
    {
        public IAtomDataSelectionStationBoundryService StationBoundry { get; }
        public IAtomDataSelectionLocalAuthoritiesService LocalAuthorities { get; }
        public IAtomDataSelectionHourlyFetchService HourlyFetch { get; }

        public AtomDataSelectionServices(
            IAtomDataSelectionStationBoundryService stationBoundry,
            IAtomDataSelectionLocalAuthoritiesService localAuthorities,
            IAtomDataSelectionHourlyFetchService hourlyFetch)
        {
            StationBoundry = stationBoundry;
            LocalAuthorities = localAuthorities;
            HourlyFetch = hourlyFetch;
        }
    }
}
