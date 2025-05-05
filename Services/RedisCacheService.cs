using StackExchange.Redis;
using System.Text.Json;
using MyTts.Config;
using Microsoft.Extensions.Options;

namespace MyTts.Services
{
    public interface IRedisCacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<bool> RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);
    }

    public class RedisCacheService : IRedisCacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly RedisConfig _config;
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(
            IConnectionMultiplexer redis,
            IOptions<RedisConfig> config,
            ILogger<RedisCacheService> logger)
        {
            _redis = redis;
            _config = config.Value;
            _logger = logger;
            _db = redis.GetDatabase(_config.DatabaseId);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var value = await _db.StringGetAsync(GetKey(key));
                return value.HasValue 
                    ? JsonSerializer.Deserialize<T>(value!) 
                    : default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value for key: {Key}", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                await _db.StringSetAsync(
                    GetKey(key),
                    serializedValue,
                    expiry ?? TimeSpan.FromMinutes(_config.DefaultExpirationMinutes)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            return await _db.KeyDeleteAsync(GetKey(key));
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _db.KeyExistsAsync(GetKey(key));
        }

        private string GetKey(string key) => $"{_config.InstanceName}{key}";
    }
}