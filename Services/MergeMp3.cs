// using System.Diagnostics;
// using System.Text.Json;
// using System.Text.Json.Serialization;

// namespace MyTts.Services
// {
//     public class MergeMp3 
//     {
//         private readonly ILogger<MergeMp3>? _logger;

//         public MergeMp3()
//         {
//         }
//         public MergeMp3(ILogger<MergeMp3> logger)
//         {
//             _logger = logger;
//         }

//         /// <summary>
//         /// Handles the given files and creates a final MP3 file at the specified file path.
//         /// </summary>
//         /// <param name="files">The array of files to be processed.</param>
//         /// <param name="filePath">The file path where the final MP3 file will be saved.</param>
//         /// <param name="appendFile">(Optional) A file to be appended to the array of files.</param>
//         /// <param name="headFile">(Optional) A file to be added at the beginning of the array of files.</param>
//         /// <param name="appendEvery">(Optional) The number of files after which the appendFile should be inserted.</param>
//         /// <returns>The path of the final MP3 file.</returns>
//         public async Task<string> HandleAsync(string[] files, string filePath, string? appendFile = null, string? headFile = null, int appendEvery = 0)
//         {
//             // Ensure all file paths are absolute
//             var fileList = files.Select(Path.GetFullPath).ToList();
//             // Insert appendFile at specified intervals
//             if (!string.IsNullOrEmpty(appendFile))
//             {
//                 fileList = InsertAppendFile(fileList, appendFile, appendEvery);
//             }
//             // Add headFile at the beginning
//             if (!string.IsNullOrEmpty(headFile))
//             {
//                 fileList.Insert(0, headFile);
//             }
//             // Ensure the directory for the output file exists
//             await CreateDirectoryIfNotExistsAsync(Path.GetDirectoryName(filePath) ?? string.Empty);
//             // Merge files using FFmpeg
//             if (!File.Exists(filePath))
//             {
//                 var success = await MergeFilesWithFfmpeg(fileList, filePath);
//                 if (!success)
//                 {
//                     throw new Exception("Failed to merge MP3 files.");
//                 }
//             }
//             return filePath;    
//         }

//         /// <summary>
//         /// Inserts the appendFile into the list of files at specified intervals.
//         /// </summary>
//         private List<string> InsertAppendFile(List<string> files, string appendFile, int appendEvery)
//         {
//             var result = new List<string>();
//             for (int i = 0; i < files.Count; i++)
//             {
//                 result.Add(files[i]);
//                 if ((i + 1) % appendEvery == 0)
//                 {
//                     result.Add(appendFile);
//                 }
//             }

//             // Remove the last appended file if it contains "newsbreak"
//             if (result.LastOrDefault()?.Contains("newsbreak") == true)
//             {
//                 result.RemoveAt(result.Count - 1);
//             }

//             return result;
//         }

//         /// <summary>
//         /// Merges the given MP3 files into a single file using FFmpeg.
//         /// This method creates a temporary file list and executes the FFmpeg command to concatenate the files.
//         /// It also handles logging of the FFmpeg output and errors.
//         /// </summary>
//         private async Task<bool> MergeFilesWithFfmpeg(List<string> files, string outputFilePath)
//         {
//             try
//             {
//                 // Validate audio streams first
//                 foreach (var file in files)
//                 {
//                     if (!await ValidateAudioStream(file))
//                     {
//                         _logger?.LogError("Invalid or no audio stream found in file: {File}", file);
//                         return false;
//                     }
//                 }
//                 var inputFiles = string.Join("|", files.Select(f => $"file '{f}'"));
//                 var tempFileList = Path.GetTempFileName();

//                 // Write the list of input files to a temporary file
//                 await File.WriteAllTextAsync(tempFileList, inputFiles);

//                 // Build the FFmpeg command
//                 var arguments = $"-f concat -safe 0 -i \"{tempFileList}\" -c copy \"{outputFilePath}\"";

//                 // Execute the FFmpeg command
//                 using var process = new Process
//                 {
//                     StartInfo = new ProcessStartInfo
//                     {
//                         FileName = "ffmpeg",
//                         Arguments = arguments,
//                         RedirectStandardOutput = true,
//                         RedirectStandardError = true,
//                         UseShellExecute = false,
//                         CreateNoWindow = true
//                     }
//                 };


//                 process.Start();
//                 string output = await process.StandardOutput.ReadToEndAsync();
//                 string error = await process.StandardError.ReadToEndAsync();
//                 await process.WaitForExitAsync();

//                 // Log the FFmpeg output and errors
//                 _logger?.LogInformation("FFmpeg Output: {Output}", output);
//                 if (!string.IsNullOrEmpty(error))
//                 {
//                     _logger?.LogError("FFmpeg Error: {Error}", error);
//                 }

//                 // Clean up the temporary file
//                 File.Delete(tempFileList);
//                 return process.ExitCode == 0;
//             }
//             catch (Exception ex)
//             {
//                 _logger?.LogError(ex, "An error occurred while merging MP3 files.");
//                 throw;
//             }
//         }

//         /// <summary>
//         /// Creates a directory if it does not already exist.
//         /// </summary>
//         // private void CreateDirectoryIfNotExists(string? directoryPath)
//         // {
//         //     if (string.IsNullOrEmpty(directoryPath))
//         //     {
//         //         return;
//         //     }

//         //     if (!Directory.Exists(directoryPath))
//         //     {
//         //         Directory.CreateDirectory(directoryPath);
//         //     }
//         // }
//         private async Task CreateDirectoryIfNotExistsAsync(string directoryPath)
//         {
//             if (!Directory.Exists(directoryPath))
//             {
//                 await Task.Run(() => Directory.CreateDirectory(directoryPath));
//             }
//         }

//         private async Task<bool> ValidateAudioStream(string filePath)
//         {
//             try
//             {
//                 var arguments = $"-i \"{filePath}\" -show_streams -select_streams a -of json";

//                 using var process = new Process
//                 {
//                     StartInfo = new ProcessStartInfo
//                     {
//                         FileName = "ffprobe",
//                         Arguments = arguments,
//                         RedirectStandardOutput = true,
//                         RedirectStandardError = true,
//                         UseShellExecute = false,
//                         CreateNoWindow = true
//                     }
//                 };

//                 process.Start();
//                 string output = await process.StandardOutput.ReadToEndAsync();
//                 await process.WaitForExitAsync();

//                 if (process.ExitCode != 0)
//                 {
//                     return false;
//                 }

//                 // Parse FFprobe JSON output
//                 var streamInfo = JsonSerializer.Deserialize<FFprobeOutput>(output);
//                 return streamInfo?.Streams?.Any(s => s.CodecType == "audio") ?? false;
//             }
//             catch (Exception ex)
//             {
//                 _logger?.LogError(ex, "Failed to validate audio stream for file: {File}", filePath);
//                 return false;
//             }
//         }
//     }

//     public class FFprobeOutput
//     {
//         public List<StreamInfo>? Streams { get; set; }
//     }

//     public class StreamInfo
//     {
//         [JsonPropertyName("codec_type")]
//         public string? CodecType { get; set; }

//         [JsonPropertyName("codec_name")]
//         public string? CodecName { get; set; }

//         [JsonPropertyName("sample_rate")]
//         public string? SampleRate { get; set; }

//         [JsonPropertyName("channels")]
//         public int Channels { get; set; }
//     }
// }