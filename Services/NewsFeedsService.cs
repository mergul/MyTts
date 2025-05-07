using System.Text.Json;
using MyTts.Models;

namespace MyTts.Services
{
    public class NewsFeedsService
    {
        private readonly ILogger<NewsFeedsService> _logger;
        private readonly IHttpClientFactory _clientFactory;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public NewsFeedsService(
            ILogger<NewsFeedsService> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }
        public string GetFeedUrl(string language)
        {
            // Replace with logic to fetch the feed URL from configuration
            return $"https://example.com/feed/{language}";
        }
        public async Task<List<string>> FetchContentsFromExternalServiceAsync(string language, int limit)
        {
            // TODO: Implement actual content fetching logic
            return await Task.FromResult(new List<string>
               {
                   "Hello world!",
                   "This is an async TTS demo using ElevenLabs and .NET.",
                   "Saving audio to local disk and Google Cloud Storage."
               });
        }
        private async Task<List<FeedItem>> FetchFeedDataAsync(string url)
        {
            using var feedClient = _clientFactory.CreateClient("FeedClientName");
            using var request = CreateRequestMessage(url);
            using var response = await feedClient.SendAsync(request).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            return await JsonSerializer.DeserializeAsync<List<FeedItem>>(
            stream, _jsonOptions).ConfigureAwait(false) ?? new List<FeedItem>();
        }
        private static HttpRequestMessage CreateRequestMessage(string url) =>
            new(HttpMethod.Get, url)
            {
                Headers =
                {
                    { "Cache-Control", "no-cache, no-store, must-revalidate" },
                    { "Pragma", "no-cache" },
                    { "Expires", "0" }
                }
            };
        private static List<Dictionary<string, object>> ProcessFeedData(IEnumerable<FeedItem> data, int limit) =>
        data.Where(item => item.Category != 0)
            .Take(limit)
            .Select(item => new Dictionary<string, object>
            {
                ["Category"] = item.Category,
                ["Title"] = item.Title,
                ["Content"] = item.Content
            })
            .ToList();

        public async Task<List<string>> GetFeedByLanguageAsync(string language, int limit)
        {
            var feedUrl = GetFeedUrl(language);
            var feedData = await FetchFeedDataAsync(feedUrl).ConfigureAwait(false);
            var processedData = ProcessFeedData(feedData, limit);
            return [.. processedData.Select(item => item["Content"].ToString())];

            // return await ConvertToMp3Async<Dictionary<string, object>>(processedData, language).ConfigureAwait(false);
        }
    }
    public record FeedItem
    {
        public int Category { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }
}
