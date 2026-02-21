using FileManager.Application.Commands;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.DeleteTempFiles;

public class DeleteAllTempFilesCommandHandler : IRequestHandler<DeleteAllTempFilesCommand, int>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<DeleteAllTempFilesCommandHandler> _logger;

    public DeleteAllTempFilesCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<DeleteAllTempFilesCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<int> Handle(DeleteAllTempFilesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.DeleteAllTempFilesAsync(cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting all temporary files");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
