using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.UpdateFile;

public class ToggleArchiveFileCommandHandler : IRequestHandler<ToggleArchiveFileCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<ToggleArchiveFileCommandHandler> _logger;

    public ToggleArchiveFileCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<ToggleArchiveFileCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<FileManagerResponse> Handle(ToggleArchiveFileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.ToggleArchiveStatusAsync(request.Id, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while toggling archive status for file {FileId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
