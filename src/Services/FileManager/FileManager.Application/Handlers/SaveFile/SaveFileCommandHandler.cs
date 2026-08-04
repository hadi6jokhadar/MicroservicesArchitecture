using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Constants;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.SaveFile;

public class SaveFileCommandHandler : IRequestHandler<SaveFileCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<SaveFileCommandHandler> _logger;

    public SaveFileCommandHandler(
        IFileManagerService fileManagerService,
        IFeatureFlagService featureFlagService,
        ILogger<SaveFileCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task<FileManagerResponse> Handle(SaveFileCommand request, CancellationToken cancellationToken)
    {
        FileManagerResponse savedFile;
        try
        {
            savedFile = await _fileManagerService.SaveFileAsync(
                request.File,
                request.Group,
                request.UserId,
                cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var fileName = request.File?.FileName ?? "Unknown";
            _logger.LogError(ex, "An error occurred while saving file {FileName}", fileName);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }

        if (_featureFlagService.IsEnabled(FeatureFlags.AutoUploadToExternalStorageEnabled, defaultValue: false))
        {
            // Awaited before returning, so on success the response already reflects the
            // populated ExternalUrl instead of forcing the caller to re-fetch the file.
            savedFile = await TryAutoUploadToBlobAsync(savedFile, cancellationToken);
        }

        return savedFile;
    }

    // Auto-upload is a best-effort side effect of the local save — the local save has
    // already succeeded by the time this runs, so a tenant without blob storage configured
    // (the common, expected case) or any other blob failure must never fail the request.
    private async Task<FileManagerResponse> TryAutoUploadToBlobAsync(FileManagerResponse savedFile, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.UploadToBlobAsync(savedFile.Id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(
                "Skipped auto-upload-to-blob for file {FileId}: {Reason}",
                savedFile.Id,
                ex.Message);
            return savedFile;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Auto-upload-to-blob failed for file {FileId} after successful local save",
                savedFile.Id);
            return savedFile;
        }
    }
}
