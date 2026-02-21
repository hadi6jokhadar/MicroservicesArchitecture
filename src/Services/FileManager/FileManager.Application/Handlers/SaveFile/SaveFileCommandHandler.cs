using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.SaveFile;

public class SaveFileCommandHandler : IRequestHandler<SaveFileCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<SaveFileCommandHandler> _logger;

    public SaveFileCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<SaveFileCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<FileManagerResponse> Handle(SaveFileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.SaveFileAsync(
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
    }
}
