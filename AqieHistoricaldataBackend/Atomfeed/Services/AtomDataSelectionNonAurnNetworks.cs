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
        IAmazonS3 S3Client
    ) : IAtomDataSelectionNonAurnNetworks
    {
        public async Task<dynamic> GetAtomNonAurnNetworks(QueryStringData data)
        {
            try
            {
                string pollutantName = data.pollutantName ?? string.Empty;

                if (data.SiteName == "Pollutant")
                {
                    await ExceltoMongoDB(pollutantName);
                }
                else
                {
                    await ExceltoMongoDB_Station_detials(pollutantName);
                }

                return "Success";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomNonAurnNetworks");
                return "Failure";
            }
        }

        public async Task ExceltoMongoDB(string pollutantName)
        {
            try
            {
                Logger.LogInformation("Pollutant Master ExceltoMongoDB Started");

                string bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
                    ?? throw new InvalidOperationException("Environment variable 'S3_BUCKET_NAME' is not configured.");

                string s3Key = Environment.GetEnvironmentVariable("POLLUTANT_MASTER_KEY")
                    ?? throw new InvalidOperationException("Environment variable 'POLLUTANT_MASTER_KEY' is not configured.");

                using var s3Response = await S3Client.GetObjectAsync(bucketName, s3Key);
                using var memoryStream = new MemoryStream();
                await s3Response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheet(1);
                var allRows = worksheet.RangeUsed().RowsUsed().ToList();

                if (allRows.Count == 0) return;

                var collection = MongoDbClientFactory.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_pollutant_master");

                var indexKeys = Builders<BsonDocument>.IndexKeys
                    .Ascending("pollutantID")
                    .Ascending("pollutantName")
                    .Ascending("pollutant_value");

                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions { Unique = true })
                );

                string[] headers = allRows[0].Cells().Select(c => c.Value.ToString()).ToArray();
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

                    var missingKeys = UniqueKeyFields.Where(k => !doc.Contains(k)).ToList();
                    if (missingKeys.Count > 0)
                    {
                        Console.WriteLine($"Skipping row – missing key field(s): {string.Join(", ", missingKeys)}");
                        continue;
                    }

                    var filter = Builders<BsonDocument>.Filter.And(
                        UniqueKeyFields.Select(k => Builders<BsonDocument>.Filter.Eq(k, doc[k]))
                    );

                    var options = new ReplaceOptions { IsUpsert = true };
                    await collection.ReplaceOneAsync(filter, doc, options);
                    upserted++;
                }

                Console.WriteLine($"Upserted {upserted} documents into MongoDB.");
                Logger.LogInformation("Upserted ExceltoMongoDB {UpsertedCount} documents into MongoDB.", upserted);
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

        public async Task ExceltoMongoDB_Station_detials(string pollutantName)
        {
            try
            {
                Logger.LogInformation("Pollutant Station Master ExceltoMongoDB Started");
                string bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
                    ?? throw new InvalidOperationException("Environment variable 'S3_BUCKET_NAME' is not configured.");

                string s3Key = Environment.GetEnvironmentVariable("POLLUTANT_STATION_MASTER_KEY")
                    ?? throw new InvalidOperationException("Environment variable 'POLLUTANT_STATION_MASTER_KEY' is not configured.");

                using var s3Response = await S3Client.GetObjectAsync(bucketName, s3Key);
                using var memoryStream = new MemoryStream();
                await s3Response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheet(1);
                var allRows = worksheet.RangeUsed().RowsUsed().ToList();

                if (allRows.Count == 0) return;

                var collection = MongoDbClientFactory.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_station_details");

                var indexKeys = Builders<BsonDocument>.IndexKeys
                    .Ascending("SiteID")
                    .Ascending("Network Type")
                    .Ascending("Pollutant Name");

                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions { Unique = true })
                );

                string[] headers = allRows[0].Cells().Select(c => c.Value.ToString()).ToArray();
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

                    var missingKeys = UniqueKeyFields.Where(k => !doc.Contains(k)).ToList();
                    if (missingKeys.Count > 0)
                    {
                        Console.WriteLine($"Skipping row – missing key field(s): {string.Join(", ", missingKeys)}");
                        continue;
                    }

                    var filter = Builders<BsonDocument>.Filter.And(
                        UniqueKeyFields.Select(k => Builders<BsonDocument>.Filter.Eq(k, doc[k]))
                    );

                    var options = new ReplaceOptions { IsUpsert = true };
                    await collection.ReplaceOneAsync(filter, doc, options);
                    upserted++;
                }

                Console.WriteLine($"Upserted {upserted} documents into MongoDB.");
                Logger.LogInformation("Upserted ExceltoMongoDB_Station_detials {UpsertedCount} documents into MongoDB.", upserted);
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
