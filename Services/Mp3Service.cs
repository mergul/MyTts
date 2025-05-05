using MyTts.Models;
using MyTts.Repositories;

namespace MyTts.Services
{
    public class Mp3Service : IMp3Service
    {
        private readonly ILogger<Mp3Service> _logger;
        private readonly IMp3FileRepository _mp3FileRepository;
        private readonly NewsFeedsService _newsFeedsService;

        public Mp3Service(
            ILogger<Mp3Service> logger,
            IMp3FileRepository mp3FileRepository,
            NewsFeedsService newsFeedsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mp3FileRepository = mp3FileRepository ?? throw new ArgumentNullException(nameof(mp3FileRepository));
            _newsFeedsService = newsFeedsService ?? throw new ArgumentNullException(nameof(newsFeedsService));
        }
        public async Task<IEnumerable<Mp3File>> GetFeedsByLanguageAsync(string language, int limit)
        {
            throw new NotImplementedException("This method is not implemented yet.");
        }
        public async Task<IEnumerable<Mp3File>> GetFeedByLanguageAsync(string language, int limit)
        {
            throw new NotImplementedException("This method is not implemented yet.");
        }

        public async Task<Mp3File> CreateSingleMp3Async(OneRequest request)
        {
            throw new NotImplementedException("This method is not implemented yet.");
        }

        public async Task<IEnumerable<Mp3File>> CreateMultipleMp3Async(ListRequest request)
        {
            throw new NotImplementedException("This method is not implemented yet.");
        }

        public async Task<Mp3File> GetMp3FileAsync(string id)
        {
            throw new NotImplementedException("This method is not implemented yet.");
        }

        public async Task<Mp3File> GetLastMp3ByLanguageAsync(string language)
        {
            throw new NotImplementedException("This method is not implemented yet.");
        }
    }
}
