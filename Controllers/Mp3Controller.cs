using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTts.Models;
using MyTts.Services;

namespace MyTts.Controllers
{
    public class Mp3Controller : ControllerBase
    {
        private readonly IMp3Service _mp3Service;
        private readonly ILogger<Mp3Controller> _logger;

        public Mp3Controller(IMp3Service mp3Service, ILogger<Mp3Controller> logger)
        {
            _mp3Service = mp3Service ?? throw new ArgumentNullException(nameof(mp3Service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        //Works Fine
        public async Task<IActionResult> Feed(string language, int limit, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _mp3Service.CreateMultipleMp3Async(language, limit, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MP3 list request");
                return Problem("Failed to process MP3 list request");
            }
        }

        public async Task<IActionResult> One(OneRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _mp3Service.CreateSingleMp3Async(request, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing single MP3 request");
                return Problem("Failed to process MP3 request");
            }
        }
        
        public async Task<IActionResult> List(ListRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var feed = await _mp3Service.GetFeedByLanguageAsync(request, cancellationToken);
                return Ok(feed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feed for language {Language}", request.Language);
                return Problem("Failed to retrieve feed");
            }
        }

        public async Task<IActionResult> GetFile(string id, CancellationToken cancellationToken)
        {
            try
            {
                var file = await _mp3Service.GetMp3FileAsync(id, cancellationToken);
                if (file == null)
                {
                    return NotFound();
                }

                return File(file.FilePath, "audio/mpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving MP3 file with ID {Id}", id);
                return Problem("Failed to retrieve MP3 file");
            }
        }

        public async Task<IActionResult> GetLast(string language, CancellationToken cancellationToken)
        {
            try
            {
                var lastFile = await _mp3Service.GetLastMp3ByLanguageAsync(language, cancellationToken);
                if (lastFile == null)
                {
                    return NotFound();
                }

                return Ok(lastFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving last MP3 for language {Language}", language);
                return Problem("Failed to retrieve last MP3");
            }
        }
        public async Task<IActionResult> GetMp3FileById(string id, CancellationToken cancellationToken)
        {
            try
            {
                var file = await _mp3Service.DownloadMp3(id, cancellationToken);
                if (file == null)
                {
                    return NotFound();
                }
                return Ok(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving MP3 file with ID {Id}", id);
                return Problem("Failed to retrieve MP3 file");
            }
        }
        public async Task<IActionResult> GetMp3FileListByLanguage(string id, CancellationToken cancellationToken)
        {
            try
            {
                var file = await _mp3Service.StreamMp3(id, cancellationToken);
                if (file == null)
                {
                    return NotFound();
                }
                return Ok(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving MP3 file with ID {Id}", id);
                return Problem("Failed to retrieve MP3 file");
            }
        }
        //public async Task Delete(string id)
        //{
        //    try
        //    {
        //        var file = await _mp3Service.GetMp3FileAsync(id);
        //        if (file == null)
        //        {
        //            await Results.NotFound().ExecuteAsync(HttpContext);
        //            return;
        //        }
        //        await _mp3Service.DeleteMp3FileAsync(file);
        //        await Results.Ok().ExecuteAsync(HttpContext);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error deleting MP3 file with ID {Id}", id);
        //        await Results.Problem("Failed to delete MP3 file").ExecuteAsync(HttpContext);
        //    }
    }
}