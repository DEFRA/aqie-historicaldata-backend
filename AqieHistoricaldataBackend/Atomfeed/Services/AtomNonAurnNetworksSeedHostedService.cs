using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomNonAurnNetworksSeedHostedService(
        ILogger<AtomNonAurnNetworksSeedHostedService> Logger,
        IAtomDataSelectionNonAurnNetworks NonAurnNetworksService,
        IMongoDbClientFactory MongoDbClientFactory
    ) : IHostedService
    {
        private const string LockCollection = "aqie_atom_seed_locks";
        private const string LockId         = "non_aurn_networks_seed";
        private const string StatusField    = "status";
        private const string StatusLocked   = "locked";
        private const string StatusDone     = "completed";
        private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(10);

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("NonAurnNetworksSeedHostedService: Starting.");

            if (!await TryAcquireLockAsync(cancellationToken))
                return;

            try
            {
                Logger.LogInformation("Seeding pollutant master data.");
                await NonAurnNetworksService.ExceltoMongoDB(string.Empty);

                Logger.LogInformation("Seeding station details data.");
                await NonAurnNetworksService.ExceltoMongoDB_Station_detials(string.Empty);

                await MarkAsCompletedAsync(cancellationToken);

                Logger.LogInformation("NonAurnNetworksSeedHostedService: Completed successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "NonAurnNetworksSeedHostedService: Seed job failed.");
                await RemoveLockAsync(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken)
        {
            try
            {
                var collection = MongoDbClientFactory.GetCollection<BsonDocument>(LockCollection);

                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(
                        Builders<BsonDocument>.IndexKeys.Ascending("acquiredAt"),
                        new CreateIndexOptions
                        {
                            ExpireAfter = LockTtl,
                            Name        = "ttl_lock"
                        }
                    ),
                    cancellationToken: cancellationToken
                );

                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("_id", LockId),
                    Builders<BsonDocument>.Filter.Ne(StatusField, StatusLocked)
                );

                var update = Builders<BsonDocument>.Update
                    .Set(StatusField,    StatusLocked)
                    .Set("acquiredAt",   DateTime.UtcNow)
                    .Set("instanceId",   Environment.MachineName)
                    .Unset("completedAt");

                var options = new FindOneAndUpdateOptions<BsonDocument>
                {
                    IsUpsert       = true,
                    ReturnDocument = ReturnDocument.After
                };

                await collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);

                Logger.LogInformation(
                    "NonAurnNetworksSeedHostedService: Lock acquired by instance '{Instance}'.",
                    Environment.MachineName
                );
                return true;
            }
            catch (MongoCommandException ex) when (ex.CodeName == "DuplicateKey")
            {
                Logger.LogInformation(ex,"NonAurnNetworksSeedHostedService: Skipping — another instance is currently seeding.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "NonAurnNetworksSeedHostedService: Failed to acquire lock.");
                return false;
            }
        }

        private async Task MarkAsCompletedAsync(CancellationToken cancellationToken)
        {
            var collection = MongoDbClientFactory.GetCollection<BsonDocument>(LockCollection);

            await collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("_id",       LockId),
                    Builders<BsonDocument>.Filter.Eq(StatusField, StatusLocked)
                ),
                Builders<BsonDocument>.Update
                    .Set(StatusField,    StatusDone)
                    .Set("completedAt",  DateTime.UtcNow)
                    .Unset("acquiredAt"),
                cancellationToken: cancellationToken
            );

            Logger.LogInformation("NonAurnNetworksSeedHostedService: Completion marker persisted.");
        }

        private async Task RemoveLockAsync(CancellationToken cancellationToken)
        {
            try
            {
                var collection = MongoDbClientFactory.GetCollection<BsonDocument>(LockCollection);
                await collection.DeleteOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("_id",       LockId),
                        Builders<BsonDocument>.Filter.Eq(StatusField, StatusLocked)
                    ),
                    cancellationToken
                );
                Logger.LogInformation("NonAurnNetworksSeedHostedService: Lock removed after failure — next deployment will retry.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "NonAurnNetworksSeedHostedService: Failed to remove lock.");
            }
        }
    }
}