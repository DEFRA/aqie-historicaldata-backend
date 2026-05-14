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

        // ── Atomic lock acquisition ──────────────────────────────────────────
        // Uses InsertOne with a fixed _id. MongoDB's unique _id constraint is
        // atomic — exactly ONE instance/CPU across the entire cluster will
        // succeed. All others get a DuplicateKeyException immediately.

        private async Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken)
        {
            try
            {
                var collection = MongoDbClientFactory.GetCollection<BsonDocument>(LockCollection);

                // TTL index: auto-deletes a stale "locked" doc after LockTtl
                // if the instance crashes before ReleaseLock/MarkCompleted.
                // "completed" docs have acquiredAt removed, so TTL never fires on them.
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

                // Attempt an atomic insert — only ONE instance wins across all CPUs
                var lockDoc = new BsonDocument
                {
                    ["_id"]        = LockId,
                    ["status"]     = StatusLocked,
                    ["acquiredAt"] = DateTime.UtcNow,       // TTL index watches this field
                    ["instanceId"] = Environment.MachineName
                };

                await collection.InsertOneAsync(lockDoc, cancellationToken: cancellationToken);

                // Only the instance that inserted successfully reaches here
                Logger.LogInformation(
                    "NonAurnNetworksSeedHostedService: Lock acquired by instance '{Instance}'.",
                    Environment.MachineName
                );
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // Another instance already inserted the lock doc (locked) OR
                // a previous deployment already completed (completed). Either way — skip.
                var status = await GetCurrentStatusAsync(cancellationToken);
                Logger.LogInformation(
                    "NonAurnNetworksSeedHostedService: Skipping — current status is '{Status}'.",
                    status ?? "unknown"
                );
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "NonAurnNetworksSeedHostedService: Failed to acquire lock.");
                return false;
            }
        }

        // Promotes "locked" → "completed" and removes acquiredAt
        // so the TTL index never auto-deletes the completion marker.
        private async Task MarkAsCompletedAsync(CancellationToken cancellationToken)
        {
            var collection = MongoDbClientFactory.GetCollection<BsonDocument>(LockCollection);

            await collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("_id",    LockId),
                    Builders<BsonDocument>.Filter.Eq("status", StatusLocked)
                ),
                Builders<BsonDocument>.Update
                    .Set("status",      StatusDone)
                    .Set("completedAt", DateTime.UtcNow)
                    .Unset("acquiredAt"), 
                cancellationToken: cancellationToken
            );

            Logger.LogInformation("NonAurnNetworksSeedHostedService: Completion marker persisted.");
        }

        // Called only on failure — removes the "locked" doc so the next deployment retries
        private async Task RemoveLockAsync(CancellationToken cancellationToken)
        {
            try
            {
                var collection = MongoDbClientFactory.GetCollection<BsonDocument>(LockCollection);
                await collection.DeleteOneAsync(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("_id",    LockId),
                        Builders<BsonDocument>.Filter.Eq("status", StatusLocked)
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

        private async Task<string?> GetCurrentStatusAsync(CancellationToken cancellationToken)
        {
            var collection = MongoDbClientFactory.GetCollection<BsonDocument>(LockCollection);
            var doc = await collection
                .Find(Builders<BsonDocument>.Filter.Eq("_id", LockId))
                .FirstOrDefaultAsync(cancellationToken);
            return doc?.GetValue("status", BsonNull.Value).ToString();
        }
    }
}