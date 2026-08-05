using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using TraineeManagement.api.Redis.Repository;

namespace TraineeManagement.api.Redis.Service
{
    public class RedisCacheService : IRedisCacheRepo
    {
        
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly IConnectionMultiplexer _redisConnection;

        public RedisCacheService(
            IDistributedCache cache, 
            ILogger<RedisCacheService> logger,
            IConnectionMultiplexer redisConnection
        )
        {
            _cache = cache;

            _logger = logger;

            _redisConnection = redisConnection;
        }

        public async Task<T?> GetItem<T>(string cacheKey)
        {
            string? cachedData;
            try
            {
                cachedData = await _cache.GetStringAsync(cacheKey);
                if (string.IsNullOrEmpty(cachedData))
                {
                    return default;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis is offline or timed out while getting key '{CacheKey}'. Falling back to database.", cacheKey);
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(cachedData);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to deserialize cache data for key '{CacheKey}'.", cacheKey);
                return default;
            }

        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var endpoints = _redisConnection.GetEndPoints();
                if (endpoints.Length == 0) return;

                var server = _redisConnection.GetServer(endpoints.First());
                var db = _redisConnection.GetDatabase();

                var keysToDelete = server.Keys(pattern: pattern).ToArray();
                if (keysToDelete.Length == 0) return;
                
                await db.KeyDeleteAsync(keysToDelete, CommandFlags.DemandMaster);

                _logger.LogInformation("Successfully deleted {Count} keys matching pattern '{Pattern}'.", keysToDelete.Length, pattern);
                
            }catch(Exception ex)
            {
                _logger.LogWarning(ex, "Redis error while removing keys by pattern '{Pattern}'.", pattern);
            }
        }

        public async Task RemoveItem(string key)
        {

            try
            {
                await _cache.RemoveAsync(key);

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis is offline or timed out while removing key '{Key}'.", key);
            }

        }

        public async Task SetItem<T>(string cacheKey, T item)
        {
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    //SlidingExpiration = TimeSpan.FromMinutes(2)
                };

                string serializedData = JsonSerializer.Serialize(item);

                await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis is offline or timed out while setting key '{CacheKey}'. Data was not cached.", cacheKey);
            }


        }
    }
}
