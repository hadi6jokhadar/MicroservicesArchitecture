using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Domain.Entities;
using FileManager.Domain.Enums;
using FileManager.Domain.Interfaces;
using FileManager.Infrastructure.Options;
using FileManager.Infrastructure.Storage;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;

namespace FileManager.Infrastructure.Services;

/// <summary>
/// Shared inline-vs-attachment policy for every path that serves a stored file back to a
/// client (static file middleware, blob storage, download endpoint). Kept separate from
/// content-type mapping because the same extension can need a different Content-Disposition
/// depending on whether it's safe to render inline (raster images, and SVG once sanitized —
/// see <see cref="FileManagerService.SanitizeSvgAsync"/>) or not (everything else defaults to
/// attachment since Content-Disposition only affects top-level navigation, not img/audio/
/// video/fetch subresource loads).
/// </summary>
public static class FileContentDispositionPolicy
{
    private static readonly HashSet<string> SafeInlineExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
        // Safe to serve inline because SaveFileAsync sanitizes every SVG on upload (strips
        // <script>, <foreignObject>, event-handler attributes, and javascript: URIs) before
        // it's ever persisted — an <img>-rendered SVG doesn't execute scripts anyway, and the
        // sanitization closes the remaining risk (a direct navigation/embed to the raw URL).
        ".svg"
    };

    public static bool RequiresAttachmentDisposition(string? extension) =>
        string.IsNullOrEmpty(extension) || !SafeInlineExtensions.Contains(extension);
}

public class FileManagerService : IFileManagerService
{
    private readonly IFileManagerRepository _repository;
    private readonly IFileManagerUsageRepository _usageRepository;
    private readonly IFileStorage _fileStorage;
    private readonly BlobStorageFactory _blobStorageFactory;
    private readonly FileManagerOptions _options;
    private readonly ILogger<FileManagerService> _logger;
    private readonly ILocalizationService _localizationService;
    private readonly string _urlPrefix;

    public FileManagerService(
        IFileManagerRepository repository,
        IFileManagerUsageRepository usageRepository,
        IFileStorage fileStorage,
        BlobStorageFactory blobStorageFactory,
        IOptions<FileManagerOptions> options,
        ILogger<FileManagerService> logger,
        ILocalizationService localizationService)
    {
        _repository = repository;
        _usageRepository = usageRepository;
        _fileStorage = fileStorage;
        _blobStorageFactory = blobStorageFactory;
        _options = options.Value;
        _logger = logger;
        _localizationService = localizationService;
        _urlPrefix = _options.RootStoragePath?.TrimEnd('/') ?? string.Empty;
    }

    public async Task<FileManagerResponse> SaveFileAsync(
        IFormFile file,
        FileGroup group,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        // Validate file
        if (file == null || file.Length == 0)
        {
            throw new BadRequestException(LocalizationKeys.Exceptions.FileEmpty);
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw new Domain.Exceptions.FileValidationException(LocalizationKeys.Exceptions.FileSizeExceeded, _localizationService);
        }

        var fileToSave = file;
        var convertedStream = (MemoryStream?)null;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension))
        {
            throw new Domain.Exceptions.FileValidationException(LocalizationKeys.Exceptions.InvalidFileType, _localizationService);
        }

        // Sniff magic bytes against the claimed extension on the raw upload, before any
        // conversion — an extension that doesn't match its actual content is exactly what
        // makes content-type-confusion attacks (e.g. renamed SVG/HTML) practical.
        if (!await MatchesKnownFileSignatureAsync(file, extension, cancellationToken))
        {
            throw new Domain.Exceptions.FileValidationException(LocalizationKeys.Exceptions.InvalidFileType, _localizationService);
        }

        // Sanitize SVG content before it's ever persisted — strips <script>, <foreignObject>,
        // event-handler attributes, and javascript: URIs, so the stored file is safe to serve
        // inline (see FileContentDispositionPolicy) rather than forced to download.
        if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            var sanitizedResult = await SanitizeSvgAsync(fileToSave, cancellationToken);
            fileToSave = sanitizedResult.SanitizedFile;
            convertedStream = sanitizedResult.SanitizedStream;
        }

        // Convert WebM audio to MP3 before save (video webm is stored as-is).
        // Falls back to saving as webm when FFmpeg is unavailable or conversion fails.
        if (string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase)
            && file.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(ResolveFfmpegExecutablePath()))
            {
                try
                {
                    var convertedResult = await ConvertWebMFormFileToMp3Async(file, cancellationToken);
                    fileToSave = convertedResult.ConvertedFile;
                    convertedStream = convertedResult.ConvertedStream;
                    extension = ".mp3";
                }
                catch (GeneralException ex)
                {
                    _logger.LogWarning(ex, "FFmpeg conversion failed for {FileName}, saving audio webm as-is", file.FileName);
                }
            }
            else
            {
                _logger.LogWarning("FFmpeg not found; audio webm {FileName} will be saved as-is", file.FileName);
            }
        }

        try
        {
            // Map extension to FileType
            var fileType = MapExtensionToFileType(extension, fileToSave.ContentType);

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";

            // Build path: {userId}/{category}/{filename} or system/{category}/{filename}
            var category = fileType.ToString().ToLowerInvariant();
            var relativePath = userId.HasValue
                ? Path.Combine(userId.Value.ToString(), category, uniqueFileName)
                : Path.Combine("system", category, uniqueFileName);

            // Save file to storage
            var savedPath = await _fileStorage.SaveAsync(fileToSave, relativePath, cancellationToken);

            // Create entity
            var entity = new FileManagerEntity
            {
                Name = Path.GetFileNameWithoutExtension(fileToSave.FileName),
                Extension = extension,
                Size = fileToSave.Length,
                Path = savedPath,
                Group = group,
                Type = fileType,
                Temp = true,
                Status = true,
                IsArchived = false,
                UserId = userId,
                Created = DateTime.UtcNow
            };

            var savedEntity = await _repository.AddAsync(entity, cancellationToken);

            _logger.LogInformation("File saved successfully: ID={Id}, Name={Name}, Path={Path}",
                savedEntity.Id, savedEntity.Name, savedEntity.Path);

            return FileManagerResponse.MapFrom(savedEntity, _urlPrefix);
        }
        finally
        {
            convertedStream?.Dispose();
        }
    }

    /// <summary>
    /// Strips the parts of an SVG that can execute attacker code — &lt;script&gt; elements,
    /// &lt;foreignObject&gt; (arbitrary embedded HTML), every event-handler attribute
    /// (onload/onclick/... ), and javascript: URIs in href/xlink:href — before the file is
    /// ever persisted. Parses with DTD processing prohibited and no XmlResolver so a crafted
    /// SVG can't use an XML external entity to read local files or make the server issue
    /// outbound requests (XXE). Rejects the upload outright if it isn't well-formed XML.
    /// </summary>
    private async Task<(IFormFile SanitizedFile, MemoryStream SanitizedStream)> SanitizeSvgAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        XDocument doc;
        try
        {
            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            doc = XDocument.Load(xmlReader, LoadOptions.None);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new Domain.Exceptions.FileValidationException(LocalizationKeys.Exceptions.InvalidFileType, _localizationService);
        }

        if (doc.Root == null)
        {
            throw new Domain.Exceptions.FileValidationException(LocalizationKeys.Exceptions.InvalidFileType, _localizationService);
        }

        foreach (var dangerousElement in doc.Descendants()
                     .Where(e => e.Name.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase)
                              || e.Name.LocalName.Equals("foreignObject", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            dangerousElement.Remove();
        }

        foreach (var element in doc.Descendants().ToList())
        {
            foreach (var attribute in element.Attributes()
                         .Where(a => a.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                                  || (a.Name.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase)
                                      && a.Value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)))
                         .ToList())
            {
                attribute.Remove();
            }
        }

        var sanitizedStream = new MemoryStream();
        doc.Save(sanitizedStream);
        sanitizedStream.Position = 0;

        var sanitizedFile = new FormFile(sanitizedStream, 0, sanitizedStream.Length, "file", file.FileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = file.ContentType
        };

        return (sanitizedFile, sanitizedStream);
    }

    private async Task<(IFormFile ConvertedFile, MemoryStream ConvertedStream)> ConvertWebMFormFileToMp3Async(
        IFormFile webmFile,
        CancellationToken cancellationToken)
    {
        var tempInputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.webm");
        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp3");

        try
        {
            await using (var tempInputStream = new FileStream(tempInputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await webmFile.CopyToAsync(tempInputStream, cancellationToken);
            }

            var ffmpegExecutable = ResolveFfmpegExecutablePath();

            if (string.IsNullOrWhiteSpace(ffmpegExecutable))
            {
                _logger.LogError("FFmpeg executable was not found. Configure FileManagerOptions:FfmpegPath or install ffmpeg and ensure it is available in PATH.");
                throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = $"-y -i \"{tempInputPath}\" -vn -acodec libmp3lame \"{tempOutputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            // Read output concurrently with waiting for exit — the original sequential
            // ReadToEndAsync() with no timeout would hang forever on a crafted file that makes
            // ffmpeg stall without closing its output pipes, regardless of the caller's token.
            var stdOutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stdErrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

            var timeoutSeconds = _options.FfmpegTimeoutSeconds > 0 ? _options.FfmpegTimeoutSeconds : 60;
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Never leave ffmpeg running unbounded — kill the whole tree so a stalled
                // child process can't keep holding worker/file-handle resources.
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Already exited between cancellation and the kill attempt.
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                _logger.LogError(
                    "FFmpeg conversion timed out after {TimeoutSeconds}s for file {FileName} and was killed",
                    timeoutSeconds, webmFile.FileName);
                throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
            }

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            if (process.ExitCode != 0 || !File.Exists(tempOutputPath))
            {
                _logger.LogError(
                    "FFmpeg conversion failed for file {FileName}. ExitCode={ExitCode}, StdOut={StdOut}, StdErr={StdErr}",
                    webmFile.FileName,
                    process.ExitCode,
                    stdOut,
                    stdErr);

                throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
            }

            var convertedBytes = await File.ReadAllBytesAsync(tempOutputPath, cancellationToken);
            var convertedStream = new MemoryStream(convertedBytes);
            var convertedFileName = $"{Path.GetFileNameWithoutExtension(webmFile.FileName)}.mp3";

            var convertedFile = new FormFile(convertedStream, 0, convertedStream.Length, "file", convertedFileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "audio/mpeg"
            };

            return (convertedFile, convertedStream);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _logger.LogError(ex, "Failed to convert webm file {FileName} to mp3", webmFile.FileName);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
        finally
        {
            try
            {
                if (File.Exists(tempInputPath))
                {
                    File.Delete(tempInputPath);
                }

                if (File.Exists(tempOutputPath))
                {
                    File.Delete(tempOutputPath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to cleanup temporary conversion files for {FileName}", webmFile.FileName);
            }
        }
    }

    private string? ResolveFfmpegExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_options.FfmpegPath))
        {
            var configuredPath = _options.FfmpegPath.Trim();

            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }

            if (string.Equals(configuredPath, "ffmpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configuredPath, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                var fromPath = FindExecutableInPath("ffmpeg.exe") ?? FindExecutableInPath("ffmpeg");
                if (!string.IsNullOrWhiteSpace(fromPath))
                {
                    return fromPath;
                }
            }
        }

        var executableFromPath = FindExecutableInPath("ffmpeg.exe") ?? FindExecutableInPath("ffmpeg");
        if (!string.IsNullOrWhiteSpace(executableFromPath))
        {
            return executableFromPath;
        }

        var commonWindowsCandidates = new[]
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe"
        };

        return commonWindowsCandidates.FirstOrDefault(File.Exists);
    }

    private static string? FindExecutableInPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var pathEntry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(pathEntry.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    public async Task<FileManagerResponse?> GetFileByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity != null ? FileManagerResponse.MapFrom(entity, _urlPrefix) : null;
    }

    public async Task<List<FileManagerResponse>> GetFilesByIdsAsync(List<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
        {
            return new List<FileManagerResponse>();
        }

        var entities = await _repository.GetByIdsAsync(ids, cancellationToken);
        return entities.Select(e => FileManagerResponse.MapFrom(e, _urlPrefix)).ToList();
    }

    public async Task<PaginatedList<FileManagerResponse>> GetFilesAsync(
        FileManagerListRequest request,
        CancellationToken cancellationToken = default)
    {
        // Apply default values if not provided
        var sortBy = request.SortBy ?? "Id";
        var ascending = request.Ascending ?? true;

        var (items, totalCount) = await _repository.GetAllAsync(
            id: request.Id,
            status: request.Status,
            isArchived: request.IsArchived,
            from: request.From,
            to: request.To,
            textFilter: request.TextFilter,
            group: request.Group,
            type: request.Type,
            temp: request.Temp,
            userId: request.UserId,
            sortBy: sortBy,
            ascending: ascending,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return new PaginatedList<FileManagerResponse>
        {
            Items = items.Select(e => FileManagerResponse.MapFrom(e, _urlPrefix)).ToList(),
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<FileManagerResponse> UpdateFileAsync(
        int id,
        string? name = null,
        FileGroup? group = null,
        bool? status = null,
        bool? isArchived = null,
        bool? temp = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id, _localizationService);
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(name))
            entity.Name = name;

        if (group.HasValue)
            entity.Group = group.Value;

        if (status.HasValue)
            entity.Status = status.Value;

        if (isArchived.HasValue)
            entity.IsArchived = isArchived.Value;

        if (temp.HasValue)
            entity.Temp = temp.Value;

        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("File updated successfully: ID={Id}", id);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<FileManagerResponse?> UpdateFileTempStatusAsync(
        int fileId,
        string usageArea,
        string rowId,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(fileId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("File with ID {FileId} not found for temp status update", fileId);
            return null;
        }

        var existingUsage = await _usageRepository.GetUsageAsync(fileId, usageArea, rowId, cancellationToken);

        if (isNew)
        {
            // Add usage row if it doesn't already exist
            if (existingUsage == null)
            {
                var usage = new FileManagerUsageEntity
                {
                    FileId = fileId,
                    UsageArea = usageArea,
                    RowId = rowId
                };
                await _usageRepository.AddAsync(usage, cancellationToken);
                _logger.LogInformation("Added usage row for FileId={FileId} UsageArea={UsageArea} RowId={RowId}", fileId, usageArea, rowId);
            }
        }
        else
        {
            // Remove usage row if it exists
            if (existingUsage != null)
            {
                await _usageRepository.RemoveAsync(existingUsage, cancellationToken);
                _logger.LogInformation("Removed usage row for FileId={FileId} UsageArea={UsageArea} RowId={RowId}", fileId, usageArea, rowId);
            }
        }

        // Recalculate temp status based on remaining usages
        var usageCount = await _usageRepository.CountUsagesAsync(fileId, cancellationToken);
        entity.Temp = usageCount == 0;

        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("File temp status updated: ID={FileId} Temp={Temp} (usages={Count})", fileId, entity.Temp, usageCount);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<FileManagerResponse> ToggleArchiveStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id, _localizationService);
        }

        entity.IsArchived = !entity.IsArchived;
        entity.LastModified = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        
        _logger.LogInformation("File archive status toggled: ID={Id}, IsArchived={IsArchived}", id, entity.IsArchived);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<bool> DeleteFileAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("File with ID {Id} not found for deletion", id);
            return false;
        }

        // Delete from blob storage if an external URL exists
        if (!string.IsNullOrWhiteSpace(entity.ExternalUrl))
        {
            try
            {
                var objectKey = ExtractObjectKeyFromExternalUrl(entity.ExternalUrl);
                var blob = _blobStorageFactory.Create();
                if (blob.IsConfigured)
                {
                    await blob.DeleteAsync(objectKey, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file from blob storage. FileId: {Id}, ExternalUrl: {Url}", id, entity.ExternalUrl);
            }
        }

        // Delete from local storage
        await _fileStorage.DeleteAsync(entity.Path, cancellationToken);

        // Delete from database
        await _repository.DeleteAsync(entity, cancellationToken);

        _logger.LogInformation("File deleted successfully: ID={Id}, Path={Path}", id, entity.Path);

        return true;
    }

    public async Task<int> DeleteAllTempFilesAsync(CancellationToken cancellationToken = default)
    {
        var tempFiles = await _repository.GetTempFilesAsync(cancellationToken);

        foreach (var file in tempFiles)
        {
            try
            {
                await DeleteBlobIfPresentAsync(file, cancellationToken);
                await _fileStorage.DeleteAsync(file.Path, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file from storage: {Path}", file.Path);
            }
        }

        var deletedCount = await _repository.DeleteAllTempAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} temporary files", deletedCount);

        return deletedCount;
    }

    public async Task<int> DeleteOldTempFilesAsync(int olderThanDays, int aiOlderThanDays = 30, CancellationToken cancellationToken = default)
    {
        var oldTempFiles = await _repository.GetOldTempFilesAsync(olderThanDays, aiOlderThanDays, cancellationToken);

        foreach (var file in oldTempFiles)
        {
            try
            {
                await DeleteBlobIfPresentAsync(file, cancellationToken);
                await _fileStorage.DeleteAsync(file.Path, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old temp file from storage: {Path}", file.Path);
            }
        }

        var deletedCount = await _repository.DeleteOldTempFilesAsync(olderThanDays, aiOlderThanDays, cancellationToken);

        _logger.LogInformation("Deleted {Count} old temporary files (older than {Days} days, AI files older than {AiDays} days)", deletedCount, olderThanDays, aiOlderThanDays);

        return deletedCount;
    }

    public async Task<FileManagerResponse> UploadToBlobAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id, _localizationService);
        }

        var blob = _blobStorageFactory.Create();
        if (!blob.IsConfigured)
        {
            throw new InvalidOperationException("Blob storage is not configured. Set BlobStorage settings in appsettings.json or tenant configuration.");
        }

        // Read the local file from storage
        var stream = await _fileStorage.GetAsync(entity.Path, cancellationToken);
        var contentType = GetContentType(entity.Extension);
        var objectKey = entity.Path.Replace("\\", "/");
        var contentDisposition = FileContentDispositionPolicy.RequiresAttachmentDisposition(entity.Extension)
            ? "attachment"
            : null;

        var publicUrl = await blob.UploadAsync(objectKey, stream, contentType, contentDisposition, cancellationToken);

        entity.ExternalUrl = publicUrl;
        entity.LastModified = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation("File {Id} uploaded to blob. ExternalUrl: {Url}", id, publicUrl);

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    public async Task<FileManagerResponse> RemoveFromBlobAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithArchivedAsync(id, cancellationToken);
        if (entity == null)
        {
            throw new Domain.Exceptions.FileNotFoundException(id, _localizationService);
        }

        if (!string.IsNullOrWhiteSpace(entity.ExternalUrl))
        {
            var blob = _blobStorageFactory.Create();
            if (blob.IsConfigured)
            {
                var objectKey = ExtractObjectKeyFromExternalUrl(entity.ExternalUrl);
                await blob.DeleteAsync(objectKey, cancellationToken);
            }

            entity.ExternalUrl = null;
            entity.LastModified = DateTime.UtcNow;
            await _repository.UpdateAsync(entity, cancellationToken);

            _logger.LogInformation("File {Id} removed from blob storage.", id);
        }

        return FileManagerResponse.MapFrom(entity, _urlPrefix);
    }

    /// <summary>Deletes from blob storage if the entity has an ExternalUrl set.</summary>
    private async Task DeleteBlobIfPresentAsync(FileManagerEntity entity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.ExternalUrl))
            return;

        try
        {
            var blob = _blobStorageFactory.Create();
            if (blob.IsConfigured)
            {
                var objectKey = ExtractObjectKeyFromExternalUrl(entity.ExternalUrl);
                await blob.DeleteAsync(objectKey, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove blob for file {Id}, ExternalUrl: {Url}", entity.Id, entity.ExternalUrl);
        }
    }

    /// <summary>
    /// Extracts the object key from a full public URL.
    /// e.g. https://pub-xxx.r2.dev/tenant/123/image/file.jpg → tenant/123/image/file.jpg
    /// </summary>
    private static string ExtractObjectKeyFromExternalUrl(string externalUrl)
    {
        var uri = new Uri(externalUrl);
        // Remove leading slash from path
        return uri.AbsolutePath.TrimStart('/');
    }

    // Magic numbers exist for these extensions and are reliable enough to reject on mismatch.
    // Types with no reliable signature (plain text, some Office legacy formats, SVG's XML text
    // body, etc.) are intentionally absent — see MatchesKnownFileSignatureAsync's fallback.
    private static readonly Dictionary<string, byte[][]> KnownFileSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".gif"] = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 } },
        [".bmp"] = new[] { new byte[] { 0x42, 0x4D } },
        [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },
        // .docx/.xlsx are ZIP containers under the hood, so they share the ZIP local-file-header signature.
        [".zip"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        // .webm is a subset of the Matroska/EBML container format, same signature as .mkv.
        [".webm"] = new[] { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } },
        [".mkv"] = new[] { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } },
    };

    private const int SignatureProbeLength = 12; // enough to cover the RIFF/WEBP check below

    private static async Task<bool> MatchesKnownFileSignatureAsync(IFormFile file, string extension, CancellationToken cancellationToken)
    {
        var header = new byte[SignatureProbeLength];
        // IFormFile.OpenReadStream() re-seeks to the section start on every call, so reading
        // here without disposing the stream does not disturb the later save/conversion reads.
        var probeStream = file.OpenReadStream();
        var bytesRead = await probeStream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        if (string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
        {
            return bytesRead >= 12
                && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 // "RIFF"
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50; // "WEBP"
        }

        if (!KnownFileSignatures.TryGetValue(extension, out var signatures))
        {
            // No reliable magic number for this type — be lenient rather than inventing brittle heuristics.
            return true;
        }

        return signatures.Any(sig => bytesRead >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig));
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        ".mp4" => "video/mp4",
        ".avi" => "video/x-msvideo",
        ".mkv" => "video/x-matroska",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };

    private FileType MapExtensionToFileType(string extension, string? contentType = null)
    {
        if (extension == ".webm")
        {
            if (contentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return FileType.Music;
            }

            if (contentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return FileType.Video;
            }
        }

        if (_options.ExtensionToTypeMapping.TryGetValue(extension, out var typeName))
        {
            if (Enum.TryParse<FileType>(typeName, true, out var fileType))
            {
                return fileType;
            }
        }

        // Default mapping if not found in configuration
        return extension switch
        {
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" => FileType.Music,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => FileType.Video,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" or ".webp" => FileType.Image,
            _ => FileType.Other
        };
    }
}
