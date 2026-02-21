using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.UpdateFile;

public class UpdateFileCommandHandler : IRequestHandler<UpdateFileCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<UpdateFileCommandHandler> _logger;

    public UpdateFileCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<UpdateFileCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<FileManagerResponse> Handle(UpdateFileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.UpdateFileAsync(
                request.Id,
                request.Name,
                request.Group,
                request.Status,
                request.IsArchived,
                request.Temp,
                cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating file {FileId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
