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

        public Task ExceltoMongoDB(string pollutantName) =>
            LoadExcelToMongoDbAsync(
                s3KeyEnvVar: "POLLUTANT_MASTER_KEY",
                collectionName: "aqie_atom_non_aurn_networks_pollutant_master",
                uniqueKeyFields: ["pollutantID", "pollutantName", "pollutant_value"],
                logContext: "Pollutant Master"
            );

        public Task ExceltoMongoDB_Station_detials(string pollutantName) =>
            LoadExcelToMongoDbAsync(
                s3KeyEnvVar: "POLLUTANT_STATION_MASTER_KEY",
                collectionName: "aqie_atom_non_aurn_networks_station_details",
                uniqueKeyFields: ["SiteID", "Network Type", "Pollutant Name"],
                logContext: "Pollutant Station Master"
            );

        private async Task LoadExcelToMongoDbAsync(
            string s3KeyEnvVar,
            string collectionName,
            string[] uniqueKeyFields,
            string logContext)
        {
            try
            {
                Logger.LogInformation("{LogContext} ExceltoMongoDB Started", logContext);

                string bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
                    ?? throw new InvalidOperationException("Environment variable 'S3_BUCKET_NAME' is not configured.");

                string s3Key = Environment.GetEnvironmentVariable(s3KeyEnvVar)
                    ?? throw new InvalidOperationException($"Environment variable '{s3KeyEnvVar}' is not configured.");

                using var s3Response = await S3Client.GetObjectAsync(bucketName, s3Key);
                using var memoryStream = new MemoryStream();
                await s3Response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheet(1);
                var allRows = worksheet.RangeUsed().RowsUsed().ToList();

                if (allRows.Count == 0) return;

                var collection = MongoDbClientFactory.GetCollection<BsonDocument>(collectionName);
                await collection.Database.DropCollectionAsync(collectionName);
                collection = MongoDbClientFactory.GetCollection<BsonDocument>(collectionName);

                var indexKeys = Builders<BsonDocument>.IndexKeys.Combine(
                    uniqueKeyFields.Select(k => Builders<BsonDocument>.IndexKeys.Ascending(k))
                );

                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions { Unique = true })
                );

                string[] headers = allRows[0].Cells().Select(c => c.Value.ToString()).ToArray();
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

                    var missingKeys = uniqueKeyFields.Where(k => !doc.Contains(k)).ToList();
                    if (missingKeys.Count > 0)
                    {
                        Console.WriteLine($"Skipping row – missing key field(s): {string.Join(", ", missingKeys)}");
                        continue;
                    }

                    var filter = Builders<BsonDocument>.Filter.And(
                        uniqueKeyFields.Select(k => Builders<BsonDocument>.Filter.Eq(k, doc[k]))
                    );

                    await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
                    upserted++;
                }

                Console.WriteLine($"Upserted {upserted} documents into MongoDB.");
                Logger.LogInformation("Upserted {LogContext} {UpsertedCount} documents into MongoDB.", logContext, upserted);
            }
            catch (FileNotFoundException ex)
            {
                Logger.LogError(ex, "Excel file not found in {LogContext}.", logContext);
                throw new InvalidOperationException($"Failed to load {logContext} data from S3.", ex);
            }
            catch (MongoException ex)
            {
                Logger.LogError(ex, "MongoDB error in {LogContext}.", logContext);
                throw new InvalidOperationException($"Failed to upsert {logContext} data to MongoDB.", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error in {LogContext}.", logContext);
                throw new InvalidOperationException($"An unexpected error occurred while processing {logContext} data.", ex);
            }
        }
    }
}
