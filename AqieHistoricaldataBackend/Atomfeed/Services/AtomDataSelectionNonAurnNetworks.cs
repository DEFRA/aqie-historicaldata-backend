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
        public async Task<dynamic> GetAtomNonAurnNetworks(QueryStringData data)
        {
            try
            {
                ExceltoMongoDB();
                ExceltoMongoDB_Station_detials();
                return "Success";

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomNonAurnNetworks"); 
                return "Failure";
            }
        }
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
                throw new InvalidOperationException("Failed to load pollutant master data from S3.", ex);
            }
            catch (MongoException ex)
            {
                Logger.LogError(ex, "MongoDB error in ExceltoMongoDB.");
                throw new InvalidOperationException("Failed to upsert pollutant master data to MongoDB.", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error in ExceltoMongoDB.");
                throw new InvalidOperationException("An unexpected error occurred while processing pollutant master data.", ex);
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
                Logger.LogError(ex, "Excel file not found in ExceltoMongoDB_Station_detials.");
                throw new InvalidOperationException("Failed to load station master data from S3.", ex);
            }
            catch (MongoException ex)   
            {
                Logger.LogError(ex, "MongoDB error in ExceltoMongoDB_Station_detials.");
                throw new InvalidOperationException("Failed to upsert station master data to MongoDB.", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error in ExceltoMongoDB_Station_detials.");
                throw new InvalidOperationException("An unexpected error occurred while processing station master data.", ex);
            }
        }
    }
}
