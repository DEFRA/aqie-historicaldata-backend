using Amazon.S3;
using AqieHistoricaldataBackend.Utils.Mongo;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionNonAurnNetworks(
        ILogger<HistoryexceedenceService> Logger,
        IMongoDbClientFactory MongoDbClientFactory,
        IConfiguration Configuration,
        IAmazonS3 S3Client
    ) : IAtomDataSelectionNonAurnNetworks
    {
        const int START_YEAR = 1973;
        const int END_YEAR = 2026;
        const string URL_TEMPLATE = "https://uk-air.defra.gov.uk/data/atom-dls/non-auto/{0}/atom.en.xml";
        const string OUTPUT_PATH = "output.xlsx";
        public async Task<dynamic> GetAtomNonAurnNetworks(QueryStringData data)
        {
            try
            {
                //Atomfeedextract();

                //Airwebextract();
                ExceltoMongoDB();
                ExceltoMongoDB_Station_detials();
                return "Success";

                var siteCollection = MongoDbClientFactory.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details");

                // Start with an empty filter (matches all documents)
                var filterBuilder = Builders<StationDetailDocument>.Filter;
                var filter = filterBuilder.Empty;

                // Narrow down by SiteID if provided
                if (!string.IsNullOrWhiteSpace(data.SiteId))
                    filter &= filterBuilder.Eq(d => d.SiteID, data.SiteId);

                // Narrow down by Pollutant Name if provided
                if (!string.IsNullOrWhiteSpace(data.pollutantName))
                    filter &= filterBuilder.Eq(d => d.PollutantName, data.pollutantName);

                var final_result = await siteCollection.Find(filter).ToListAsync();
                return "Success";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomNonAurnNetworks"); 
                return "Failure";
            }
        }

        public async void Atomfeedextract()
        {


            var allRecords = new List<FeedRecord>();

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            for (int year = START_YEAR; year <= END_YEAR; year++)
            {
                string url = string.Format(URL_TEMPLATE, year);
                Console.Write($"Fetching {year}... ");

                try
                {
                    string xml = await client.GetStringAsync(url);
                    var records = ParseFeed(xml, year);
                    allRecords.AddRange(records);
                    Console.WriteLine($"{records.Count} entries found.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"HTTP error - skipping. ({ex.Message})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error - skipping. ({ex.Message})");
                }

                // Small delay to avoid hammering the server
                await Task.Delay(300);
            }

            Console.WriteLine($"\nTotal records: {allRecords.Count}. Writing to {OUTPUT_PATH}...");
            ExportToExcel(allRecords, OUTPUT_PATH);
            Console.WriteLine("Done!");


        }

        public static List<FeedRecord> ParseFeed(string xmlContent, int year)
        {
            var records = new List<FeedRecord>();

            var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
            var georss = XNamespace.Get("http://www.georss.org/georss");

            XDocument doc;
            try { doc = XDocument.Parse(xmlContent); }
            catch { return records; } // skip malformed XML

            foreach (var entry in doc.Root.Elements(ns + "entry"))
            {
                var record = new FeedRecord { Year = year };

                // ── Station Name & SiteID
                string title = (string)entry.Element(ns + "title") ?? "";

                var stationMatch = Regex.Match(title, @"for (.+?) in \d{4}");
                string fullName = stationMatch.Success ? stationMatch.Groups[1].Value.Trim() : "";

                var siteMatch = Regex.Match(fullName, @"\((\w+)\)$");
                record.SiteID = siteMatch.Success ? siteMatch.Groups[1].Value : "";
                record.StationName = siteMatch.Success
                    ? fullName.Substring(0, siteMatch.Index).Trim()
                    : fullName;

                // ── Pollutants
                var pollutants = entry.Elements(ns + "link")
                    .Where(l => (string)l.Attribute("rel") == "related"
                             && ((string)l.Attribute("title") ?? "").StartsWith("Pollutant in feed - "))
                    .Select(l => ((string)l.Attribute("title"))
                                 .Replace("Pollutant in feed - ", "").Trim());

                record.Pollutants = string.Join("; ", pollutants);

                // ── Latitude & Longitude
                string polygon = (string)entry.Element(georss + "polygon") ?? "";
                var coords = polygon.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                record.Latitude = coords.Length > 0 ? coords[0] : "";
                record.Longitude = coords.Length > 1 ? coords[1] : "";

                records.Add(record);
            }

            return records;
        }

        public static void ExportToExcel(List<FeedRecord> records, string path)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Air Quality Data");

            // Header
            string[] headers = { "Year", "Station Name", "SiteID", "Pollutants", "Latitude", "Longitude" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            }

            // Data rows
            for (int r = 0; r < records.Count; r++)
            {
                var rec = records[r];
                ws.Cell(r + 2, 1).Value = rec.Year;
                ws.Cell(r + 2, 2).Value = rec.StationName;
                ws.Cell(r + 2, 3).Value = rec.SiteID;
                ws.Cell(r + 2, 4).Value = rec.Pollutants;
                ws.Cell(r + 2, 5).Value = rec.Latitude;
                ws.Cell(r + 2, 6).Value = rec.Longitude;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
        }

        public class FeedRecord
        {
            public int Year { get; set; }
            public string StationName { get; set; }
            public string SiteID { get; set; }
            public string Pollutants { get; set; }
            public string Latitude { get; set; }
            public string Longitude { get; set; }
        }

        public async void Airwebextract()
        {
            // ── Add as many UK-AIR IDs as needed ────────────────────────────────────
            //string[] UkaIds = { "UKA00451" /*, "UKA00452", ... */ };
            string[] UkaIds = { "UKA00136", "UKA00086", "UKA00451", "UKA00067", "UKA00239", "UKA01087", "UKA00114", "UKA00936", "UKA00069", "UKA00120", "UKA00055", "UKA00614", "UKA00087", "UKA00100", "UKA00101", "UKA00550", "UKA01086", "UKA00130", "UKA00103", "UKA00490", "UKA00224", "UKA00607", "UKA00154", "UKA00348", "UKA00056", "UKA00047", "UKA00169", "UKA00293", "UKA00094", "UKA00118", "UKA00268", "UKA00107", "UKA00166", "UKA00152", "UKA00357", "UKA00504", "UKA00180", "UKA00110", "UKA00173", "UKA00122", "UKA00317", "UKA00162", "UKA00112", "UKA00113", "UKA00531", "UKA00119", "UKA00123", "UKA00092", "UKA00168" };

            const string URL_TEMPLATE = "https://uk-air.defra.gov.uk/networks/site-info?uka_id={0}";
            const string OUTPUT_PATH = "site_info_output.xlsx";

            var allRecords = new List<SiteRecord>();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; DataScraper/1.0)");
            client.Timeout = TimeSpan.FromSeconds(30);

            foreach (var ukaId in UkaIds)
            {
                string url = string.Format(URL_TEMPLATE, ukaId);
                Console.Write($"Fetching {ukaId}... ");

                try
                {
                    string html = await client.GetStringAsync(url);
                    var records = ParseSitePage(html, ukaId);
                    allRecords.AddRange(records);
                    Console.WriteLine($"{records.Count} rows extracted.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error - skipping. ({ex.Message})");
                }

                await Task.Delay(300); //await Task.Delay(TimeSpan.FromSeconds(50)); // polite delay between requests
            }

            Console.WriteLine($"\nTotal rows: {allRecords.Count}. Writing to {OUTPUT_PATH}...");
            ExportToExcelSite(allRecords, OUTPUT_PATH);
            Console.WriteLine("Done!");
            // Placeholder for future implementation of Airweb data extraction
            await Task.CompletedTask;

        }

        public static List<SiteRecord> ParseSitePage(string html, string ukaId)
        {
            var records = new List<SiteRecord>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var body = doc.DocumentNode;

            // ── Walk through each network section ────────────────────────────────
            // Each network starts with an <h4> heading, followed by a <table>
            var h4Nodes = body.SelectNodes("//h4");
            if (h4Nodes == null) return records;

            foreach (var h4 in h4Nodes)
            {
                string networkName = h4.InnerText.Trim();

                // Find the next sibling <table> after this <h4>
                var sibling = h4.NextSibling;
                HtmlNode table = null;

                while (sibling != null)
                {
                    if (sibling.Name == "table") { table = sibling; break; }
                    if (sibling.Name == "h4") break; // hit next section
                    sibling = sibling.NextSibling;
                }

                if (table == null) continue;

                // Parse table rows (skip header row)
                var rows = table.SelectNodes(".//tr");
                if (rows == null || rows.Count < 2) continue;

                foreach (var row in rows.Skip(1))
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 4) continue;

                    records.Add(new SiteRecord
                    {
                        UkAirId = ukaId,
                        NetworkType = networkName,
                        Pollutant = cells[0].InnerText.Trim(),
                        StartDate = cells[1].InnerText.Trim(),
                        EndDate = cells[2].InnerText.Trim(),
                        InletHeight = cells[3].InnerText.Trim()
                    });
                }
            }

            return records;
        }

        public static void ExportToExcelSite(List<SiteRecord> records, string path)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Site Info");

            // Header
            string[] headers = { "UK-AIR ID", "Network Type", "Pollutant", "Start Date", "End Date", "Inlet Height (m)" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            }

            // Data rows
            for (int r = 0; r < records.Count; r++)
            {
                var rec = records[r];
                ws.Cell(r + 2, 1).Value = rec.UkAirId;
                ws.Cell(r + 2, 2).Value = rec.NetworkType;
                ws.Cell(r + 2, 3).Value = rec.Pollutant;
                ws.Cell(r + 2, 4).Value = rec.StartDate;
                ws.Cell(r + 2, 5).Value = rec.EndDate;
                ws.Cell(r + 2, 6).Value = rec.InletHeight;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
        }
        public class SiteRecord
        {
            public string UkAirId { get; set; }
            public string NetworkType { get; set; }
            public string Pollutant { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public string InletHeight { get; set; }
        }

        //public async void ExceltoMongoDB()
        //{           
        //    const string filePath = "/home/cdpshell/pollutants_master_updated_New.xlsx";
        //    using var workbook = new XLWorkbook(filePath);
        //    var worksheet = workbook.Worksheet(1);
        //    var allRows = worksheet.RangeUsed().RowsUsed().ToList();

        //    if (allRows.Count == 0) return;

        //    // MongoDB setup – collection is created automatically on first upsert
        //    var client = new MongoClient("mongodb://localhost:27017");
        //    var db = client.GetDatabase("aqie-historicaldata-backend");
        //    //var collection = db.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_station_details");
        //    var collection = db.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_pollutant_master");

        //    // Read headers from the first row
        //    string[] headers = allRows[0].Cells().Select(c => c.Value.ToString()).ToArray();

        //    // ── Set this to whichever column uniquely identifies a record ──────────
        //    const string UniqueKeyField = "pollutantID"; // e.g. "SiteID", "UkAirId", etc.
        //    // ───────────────────────────────────────────────────────────────────────

        //    // ── Composite unique key: all listed fields must match for an upsert ──
        //    string[] UniqueKeyFields = ["pollutantID", "pollutantName", "pollutant_value"];
        //    // ─────────────────────────────────────────────────────────────────────

        //    // ── Set this to whichever column uniquely identifies a record ──────────
        //    //const string UniqueKeyField = "SiteID"; // e.g. "SiteID", "UkAirId", etc.
        //    // ───────────────────────────────────────────────────────────────────────

        //    // ── Composite unique key: all listed fields must match for an upsert ──
        //    //string[] UniqueKeyFields = ["SiteID", "Network Type", "Pollutant Name"];
        //    // ─────────────────────────────────────────────────────────────────────

        //    int upserted = 0;

        //    foreach (var row in allRows.Skip(1))
        //    {
        //        var doc = new BsonDocument();
        //        int i = 0;
        //        foreach (var cell in row.Cells())
        //        {
        //            if (i < headers.Length)
        //                doc[headers[i]] = cell.Value.ToString();
        //            i++;
        //        }

        //        // Ensure all key fields are present in the document
        //        var missingKeys = UniqueKeyFields.Where(k => !doc.Contains(k)).ToList();
        //        if (missingKeys.Count > 0)
        //        {
        //            Console.WriteLine($"Skipping row – missing key field(s): {string.Join(", ", missingKeys)}");
        //            continue;
        //        }

        //        // Build a combined AND filter across all unique key fields
        //        var filter = Builders<BsonDocument>.Filter.And(
        //            UniqueKeyFields.Select(k => Builders<BsonDocument>.Filter.Eq(k, doc[k]))
        //        );

        //        var options = new ReplaceOptions { IsUpsert = true };
        //        await collection.ReplaceOneAsync(filter, doc, options);
        //        upserted++;
        //    }

        //    Console.WriteLine($"Upserted {upserted} documents into MongoDB.");
        //    Logger.LogInformation("Upserted {UpsertedCount} documents into MongoDB.", upserted);
        //}
        public async Task ExceltoMongoDB()
        {
            try
            {
                string bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
                    ?? throw new InvalidOperationException("Environment variable 'S3_BUCKET_NAME' is not configured.");

                string s3Key = Environment.GetEnvironmentVariable("POLLUTANT_MASTER")
                    ?? throw new InvalidOperationException("'PollutantsMasterS3Key' is not configured.");

                Logger.LogInformation("Downloading '{S3Key}' from S3 bucket '{BucketName}'.", s3Key, bucketName);

                using var s3Response = await S3Client.GetObjectAsync(bucketName, s3Key);
                using var memoryStream = new MemoryStream();
                await s3Response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheet(1);
                var allRows = worksheet.RangeUsed().RowsUsed().ToList();

                if (allRows.Count == 0) return;

                // MongoDB setup via MongoDbClientFactory
                var collection = MongoDbClientFactory.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_pollutant_master");

                // Ensure a compound index on the unique key fields for efficient upserts
                var indexKeys = Builders<BsonDocument>.IndexKeys
                    .Ascending("pollutantID")
                    .Ascending("pollutantName")
                    .Ascending("pollutant_value");

                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions { Unique = true })
                );

                // Read headers from the first row
                string[] headers = allRows[0].Cells().Select(c => c.Value.ToString()).ToArray();

                // Composite unique key: all listed fields must match for an upsert
                string[] UniqueKeyFields = ["pollutantID", "pollutantName", "pollutant_value"];

                int upserted = 0;

                foreach (var row in allRows.Skip(1))
                {
                    var doc = new BsonDocument();
                    int i = 0;
                    foreach (var cell in row.Cells())
                    {
                        if (i < headers.Length)
                            doc[headers[i]] = cell.Value.ToString();
                        i++;
                    }

                    // Ensure all key fields are present in the document
                    var missingKeys = UniqueKeyFields.Where(k => !doc.Contains(k)).ToList();
                    if (missingKeys.Count > 0)
                    {
                        Console.WriteLine($"Skipping row – missing key field(s): {string.Join(", ", missingKeys)}");
                        continue;
                    }

                    // Build a combined AND filter across all unique key fields
                    var filter = Builders<BsonDocument>.Filter.And(
                        UniqueKeyFields.Select(k => Builders<BsonDocument>.Filter.Eq(k, doc[k]))
                    );

                    var options = new ReplaceOptions { IsUpsert = true };
                    await collection.ReplaceOneAsync(filter, doc, options);
                    upserted++;
                }

                Console.WriteLine($"Upserted {upserted} documents into MongoDB.");
                Logger.LogInformation("Upserted {UpsertedCount} documents into MongoDB.", upserted);
            }
            catch (FileNotFoundException ex)
            {
                Logger.LogError(ex, "Excel file not found in ExceltoMongoDB.");
                throw;
            }
            catch (MongoException ex)
            {
                Logger.LogError(ex, "MongoDB error in ExceltoMongoDB.");
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error in ExceltoMongoDB.");
                throw;
            }
        }
        public async Task ExceltoMongoDB_Station_detials()
        {
            try
            {
                string bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
                    ?? throw new InvalidOperationException("Environment variable 'S3_BUCKET_NAME' is not configured.");

                string s3Key = Environment.GetEnvironmentVariable("POLLUTANT_STATION_MASTER")
                    ?? throw new InvalidOperationException("'StationMasterS3Key' is not configured.");

                Logger.LogInformation("Downloading '{S3Key}' from S3 bucket '{BucketName}'.", s3Key, bucketName);

                using var s3Response = await S3Client.GetObjectAsync(bucketName, s3Key);
                using var memoryStream = new MemoryStream();
                await s3Response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheet(1);
                var allRows = worksheet.RangeUsed().RowsUsed().ToList();

                if (allRows.Count == 0) return;

                // MongoDB setup via MongoDbClientFactory
                var collection = MongoDbClientFactory.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_station_details");

                // Ensure a compound index on the unique key fields for efficient upserts
                var indexKeys = Builders<BsonDocument>.IndexKeys
                    .Ascending("SiteID")
                    .Ascending("Network Type")
                    .Ascending("Pollutant Name"); //["SiteID", "Network Type", "Pollutant Name"]

                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions { Unique = true })
                );

                // Read headers from the first row
                string[] headers = allRows[0].Cells().Select(c => c.Value.ToString()).ToArray();

                // Composite unique key: all listed fields must match for an upsert
                string[] UniqueKeyFields = ["SiteID", "Network Type", "Pollutant Name"];

                int upserted = 0;

                foreach (var row in allRows.Skip(1))
                {
                    var doc = new BsonDocument();
                    int i = 0;
                    foreach (var cell in row.Cells())
                    {
                        if (i < headers.Length)
                            doc[headers[i]] = cell.Value.ToString();
                        i++;
                    }

                    // Ensure all key fields are present in the document
                    var missingKeys = UniqueKeyFields.Where(k => !doc.Contains(k)).ToList();
                    if (missingKeys.Count > 0)
                    {
                        Console.WriteLine($"Skipping row – missing key field(s): {string.Join(", ", missingKeys)}");
                        continue;
                    }

                    // Build a combined AND filter across all unique key fields
                    var filter = Builders<BsonDocument>.Filter.And(
                        UniqueKeyFields.Select(k => Builders<BsonDocument>.Filter.Eq(k, doc[k]))
                    );

                    var options = new ReplaceOptions { IsUpsert = true };
                    await collection.ReplaceOneAsync(filter, doc, options);
                    upserted++;
                }

                Console.WriteLine($"Upserted {upserted} documents into MongoDB.");
                Logger.LogInformation("Upserted {UpsertedCount} documents into MongoDB.", upserted);
            }
            catch (FileNotFoundException ex)
            {
                Logger.LogError(ex, "Excel file not found in ExceltoMongoDB.");
                throw;
            }
            catch (MongoException ex)
            {
                Logger.LogError(ex, "MongoDB error in ExceltoMongoDB.");
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error in ExceltoMongoDB.");
                throw;
            }
        }
    }
}
