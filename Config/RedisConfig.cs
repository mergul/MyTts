using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace MyTts.Config
{
    public class RedisConfig : IValidateOptions<RedisConfig>
    {
        [Required]
        public string ConnectionString { get; set; } = "localhost:6379";

        [Required]
        public string InstanceName { get; set; } = "HaberTTS_";

        public int DatabaseId { get; set; } = 0;

        public int DefaultExpirationMinutes { get; set; } = 60;

        public ValidateOptionsResult Validate(string? name, RedisConfig options)
        {
            if (string.IsNullOrEmpty(options.ConnectionString))
                return ValidateOptionsResult.Fail("Redis connection string is required");

            if (string.IsNullOrEmpty(options.InstanceName))
                return ValidateOptionsResult.Fail("Redis instance name is required");

            return ValidateOptionsResult.Success;
        }
    }
}