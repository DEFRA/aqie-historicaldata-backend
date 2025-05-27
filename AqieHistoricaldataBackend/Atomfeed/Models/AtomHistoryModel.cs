namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public class AtomHistoryModel
    {

        public class Atom_data
        {
            public string om_observedProperty { get; set; }
            public string swe_values { get; set; }
            //public List<string> swe_values {  get; set; }
        }
        public class pollutanturl
        {
            public string year { get; set; }
            public string stationname { get; set; }
            public string atom_url { get; set; }
            public string end_date { get; set; }
        }
        public class pollutantdetails
        {
            public string polluntantname { get; set; }
            public string pollutant_master_url { get; set; }
        }

        public class pollutantdata
        {
            public List<Finaldata> finaldata { get; set; }
        }
        public class Finaldata
        {
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public string Verification { get; set; }
            public string Validity { get; set; }
            public string Value { get; set; }
            //public string DataCapture { get; set; }
            public string Pollutantname { get; set; }
            //public string Stationname { get; set; }
            public string ReportDate { get; set; }
            public decimal Total { get; set; }
            public string DailyVerification { get; set; }
            public string DailyValidity { get; set; }
            public string DailyValue { get; set; }
            //public string DataCapture { get; set; }
            public string DailyPollutantname { get; set; }
            public string AnnualPollutantname { get; set; }
            public string AnnualVerification { get; set; }
        }

        public class DailyAverage
        {

            public string ReportDate { get; set; }
            public decimal Total { get; set; }
            public string Verification { get; set; }
            //public string Validity { get; set; }
            public string Value { get; set; }
            //public string DataCapture { get; set; }
            public string Pollutantname { get; set; }
            //public string Stationname { get; set; }
        }
        public class AnnualAverage
        {

            public string ReportDate { get; set; }
            public decimal Total { get; set; }
            public string Pollutantname { get; set; }
            //public string Stationname { get; set; }
        }
        public class Timelog
        {
            public DateTime xml_read_start_time { get; set; }
            public DateTime xml_read_end_time { get; set; }
            public DateTime xml_total_pollutant_reading_start_time { get; set; }
            //public DateTime xml_each_pollutant_reading_start_time { get; set; }
            //public DateTime xml_each_pollutant_reading_end_time { get; set; }
            public DateTime xml_total_pollutant_reading_end_time { get; set; }
            public DateTime xml_total_final_pollutant_split_start_time { get; set; }
            //public DateTime xml_each_final_pollutant_split_start_time { get; set; }
            //public DateTime xml_each_final_pollutant_split_end_time { get; set; }
            //public DateTime xml_csv_write_start_time { get; set; }
            //public DateTime xml_csv_write_end_time { get; set; }
            public DateTime xml_total_final_pollutant_split_end_time { get; set; }
            //public DateTime total_start_time { get; set; }
            //public DateTime total_end_time { get; set; }
        }
        public class xmlreadtimelog
        {
            public DateTime xml_each_pollutant_reading_start_time { get; set; }
            public DateTime xml_each_pollutant_reading_end_time { get; set; }
        }
        public class xmlpollutantsplittime
        {
            public DateTime xml_each_final_pollutant_split_start_time { get; set; }
            public DateTime xml_each_final_pollutant_split_end_time { get; set; }
            //public DateTime xml_csv_write_start_time { get; set; }
            //public DateTime xml_csv_write_end_time { get; set; }
        }
        public class csvwritetime
        {
            public DateTime xml_csv_write_start_time { get; set; }
            public DateTime xml_csv_write_end_time { get; set; }
        }
        public class totaltime
        {
            public DateTime total_start_time { get; set; }
            public DateTime total_end_time { get; set; }
        }

        public class dailyannualaverage
        {
            public DateTime daily_average_start_time { get; set; }
            public DateTime daily_average_end_time { get; set; }
            public DateTime annual_average_start_time { get; set; }
            public DateTime annual_average_end_time { get; set; }
        }
        public class aurnmetadata
        {
            public int groups_id { get; set; }
            public string local_site_id { get; set; }
            public string site_id { get; set; }
            public string site_name { get; set; }
            public string location_type { get; set; }
            public double latitude { get; set; }
            public double longitude { get; set; }
            public string EU_site_id { get; set; }
            public string EMEP_site_id { get; set; }
            public int zone_region { get; set; }
            public int agglomeration_region { get; set; }
            public double northing { get; set; }
            public double easting { get; set; }
            public int altitude { get; set; }
            public string group_name { get; set; }
            public string station_identifier { get; set; }


        }
        public class Rootobject
        {
            public Class1[] Property1 { get; set; }
        }

        public class Class1
        {
            public string groups_id { get; set; }
            public string local_site_id { get; set; }
            public string site_id { get; set; }
            public string site_name { get; set; }
            public string location_type { get; set; }
            public string latitude { get; set; }
            public string longitude { get; set; }
            public string EU_site_id { get; set; }
            public object EMEP_site_id { get; set; }
            public string zone_region { get; set; }
            public string agglomeration_region { get; set; }
            public string northing { get; set; }
            public string easting { get; set; }
            public string altitude { get; set; }
            public string group_name { get; set; }
            public string station_identifier { get; set; }
            public Parameter_Ids[] parameter_ids { get; set; }
        }

        public class Parameter_Ids
        {
            public string date_started { get; set; }
            public string date_ended { get; set; }
            public string parameter_id { get; set; }
            public string site_network_id { get; set; }
            public string oberservedProperty { get; set; }
            public Feature_Of_Interest[] feature_of_interest { get; set; }
            public Process[] process { get; set; }
            public string sampling_point { get; set; }
        }

        public class Feature_Of_Interest
        {
            public string featureOfInterset { get; set; }
            public object start_date { get; set; }
            public object ended_date { get; set; }
        }

        public class Process
        {
            public string process { get; set; }
            public object start_date { get; set; }
            public object ended_date { get; set; }
        }

        public class AtomURL
        {
            public string PresignedUrl { get; set; }
        }

        public class querystringdata
        {
            public string stationreaddate { get; set; }
            public string region { get; set; }
            public string siteType { get; set; }
            public string sitename { get; set; }
            public string siteId { get; set; }
            public string latitude { get; set; }

            public string longitude { get; set; }
            public string year { get; set; }
            public string downloadpollutant { get; set; }
            public string downloadpollutanttype { get; set; }
        }
        public class pivotpollutant
        {
            public string date { get; set; }
            public string time { get; set; }
            public List<SubpollutantItem> Subpollutant { get; set; }
        }
        public class SubpollutantItem
        {
            public string pollutantname { get; set; }
            public string pollutantvalue { get; set; }
            public string verification { get; set; }
        }
    }
}
