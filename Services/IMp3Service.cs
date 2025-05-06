
using Microsoft.AspNetCore.Mvc;
using MyTts.Models;

namespace MyTts.Services
{
    public interface IMp3Service
    {
        Task<IEnumerable<Mp3File>> GetFeedByLanguageAsync(string language, int limit);
        Task<Mp3File> CreateSingleMp3Async(OneRequest request);
        Task<string> CreateMultipleMp3Async(ListRequest request);
        Task<Mp3File> GetMp3FileAsync(string id);
        Task<Mp3File> GetLastMp3ByLanguageAsync(string language);
        Task<IActionResult> DownloadMp3(string id, CancellationToken cancellationToken);
        Task<IActionResult> StreamMp3(string id, CancellationToken cancellationToken);
    }
}