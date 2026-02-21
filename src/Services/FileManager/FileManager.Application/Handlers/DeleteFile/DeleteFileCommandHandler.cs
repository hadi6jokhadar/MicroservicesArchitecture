using FileManager.Application.Commands;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.DeleteFile;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, bool>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<DeleteFileCommandHandler> _logger;

    public DeleteFileCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<DeleteFileCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.DeleteFileAsync(request.Id, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting file {FileId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
