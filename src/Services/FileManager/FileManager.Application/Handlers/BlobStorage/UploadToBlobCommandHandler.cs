using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.BlobStorage;

public class UploadToBlobCommandHandler : IRequestHandler<UploadToBlobCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<UploadToBlobCommandHandler> _logger;

    public UploadToBlobCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<UploadToBlobCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<FileManagerResponse> Handle(UploadToBlobCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.UploadToBlobAsync(request.FileId, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Blob storage not configured for file {FileId}", request.FileId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while uploading file {FileId} to blob", request.FileId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
