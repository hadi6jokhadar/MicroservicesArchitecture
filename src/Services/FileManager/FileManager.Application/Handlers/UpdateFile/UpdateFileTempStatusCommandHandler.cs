using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.UpdateFile;

public class UpdateFileTempStatusCommandHandler : IRequestHandler<UpdateFileTempStatusCommand, FileManagerResponse?>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<UpdateFileTempStatusCommandHandler> _logger;

    public UpdateFileTempStatusCommandHandler(
        IFileManagerService fileManagerService,
        ILogger<UpdateFileTempStatusCommandHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<FileManagerResponse?> Handle(UpdateFileTempStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.UpdateFileTempStatusAsync(
                request.FileId,
                request.UsageArea,
                request.RowId,
                request.IsNew,
                cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating temp status for file {FileId}", request.FileId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
