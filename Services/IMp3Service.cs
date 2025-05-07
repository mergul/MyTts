
using Microsoft.AspNetCore.Mvc;
using MyTts.Data.Interfaces;
using MyTts.Models;
namespace MyTts.Services
{
    public interface IMp3Service
    {
        Task<IEnumerable<IMp3>> GetFeedByLanguageAsync(ListRequest request, CancellationToken cancellationToken);
        Task<IEnumerable<IMp3>> GetMp3FileListAsync(CancellationToken cancellationToken);
        Task<IMp3> CreateSingleMp3Async(OneRequest request, CancellationToken cancellationToken);
        Task<string> CreateMultipleMp3Async(string language, int limit, CancellationToken cancellationToken);
        Task<IMp3> GetMp3FileAsync(string id, CancellationToken cancellationToken);
        Task<IMp3> GetLastMp3ByLanguageAsync(string language, CancellationToken cancellationToken);
        Task<IActionResult> DownloadMp3(string id, CancellationToken cancellationToken);
        Task<IActionResult> StreamMp3(string id, CancellationToken cancellationToken);
        Task<IEnumerable<IMp3>> GetMp3FileListByLanguageAsync(string language, CancellationToken cancellationToken);
    }
}