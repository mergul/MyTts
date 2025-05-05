using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace MyTts.Config
{
    public class ElevenLabsConfig : IValidateOptions<ElevenLabsConfig>
    {
        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        public string VoiceId { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        [Range(0, 1)]
        public float Stability { get; set; } = 0.5f;

        [Range(0, 1)]
        public float Similarity { get; set; } = 0.75f;

        [Range(0, 1)]
        public float Style { get; set; } = 0.5f;

        public bool Boost { get; set; } = true;

        [Range(0.1, 5.0)]
        public float Speed { get; set; } = 1.0f;

        public Dictionary<string, VoiceConfig> Feed { get; set; } = new();

        public ValidateOptionsResult Validate(string? name, ElevenLabsConfig options)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(options.ApiKey))
                errors.Add("ApiKey is required");

            if (string.IsNullOrEmpty(options.VoiceId))
                errors.Add("VoiceId is required");

            if (string.IsNullOrEmpty(options.Model))
                errors.Add("Model is required");

            if (options.Stability < 0 || options.Stability > 1)
                errors.Add("Stability must be between 0 and 1");

            if (options.Similarity < 0 || options.Similarity > 1)
                errors.Add("Similarity must be between 0 and 1");

            if (options.Speed < 0.1 || options.Speed > 5.0)
                errors.Add("Speed must be between 0.1 and 5.0");
            if (options.Style < 0 || options.Style > 1)
                errors.Add("Style must be between 0 and 1");
            if (options.Boost != true && options.Boost != false)
                errors.Add("Boost must be true or false");
            if (options.Feed == null || options.Feed.Count == 0)
                errors.Add("Feed must contain at least one entry");
            else
            {
                foreach (var feed in options.Feed)
                {
                    if (string.IsNullOrEmpty(feed.Key))
                        errors.Add("Feed key cannot be null or empty");
                    if (string.IsNullOrEmpty(feed.Value.Voice))
                        errors.Add($"Voice is required for feed '{feed.Key}'");
                }
            }

            return errors.Count > 0
                ? ValidateOptionsResult.Fail(errors)
                : ValidateOptionsResult.Success;
        }
    }

    public class VoiceConfig
    {
        [Required]
        public string Voice { get; set; } = string.Empty;
    }
}