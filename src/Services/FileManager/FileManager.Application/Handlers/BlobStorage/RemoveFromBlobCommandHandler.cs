using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.BlobStorage;

public class RemoveFromBlobCommandHandler : IRequestHandler<RemoveFromBlobCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<RemoveFromBlobCommandHandler> _logger;

    public RemoveFromBlobCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<RemoveFromBlobCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<FileManagerResponse> Handle(RemoveFromBlobCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.RemoveFromBlobAsync(request.FileId, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while removing file {FileId} from blob", request.FileId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
