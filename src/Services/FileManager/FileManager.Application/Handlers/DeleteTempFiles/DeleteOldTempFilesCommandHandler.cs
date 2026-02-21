using FileManager.Application.Commands;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.DeleteTempFiles;

public class DeleteOldTempFilesCommandHandler : IRequestHandler<DeleteOldTempFilesCommand, int>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<DeleteOldTempFilesCommandHandler> _logger;

    public DeleteOldTempFilesCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<DeleteOldTempFilesCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<int> Handle(DeleteOldTempFilesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.DeleteOldTempFilesAsync(request.OlderThanDays, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting temporary files older than {Days} days", request.OlderThanDays);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
