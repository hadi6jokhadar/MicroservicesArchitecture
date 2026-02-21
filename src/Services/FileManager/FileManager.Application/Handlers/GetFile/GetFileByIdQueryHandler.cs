using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Application.Queries;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.GetFile;

public class GetFileByIdQueryHandler : IRequestHandler<GetFileByIdQuery, FileManagerResponse?>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<GetFileByIdQueryHandler> _logger;

    public GetFileByIdQueryHandler(
        IFileManagerService fileManagerService,
        ILogger<GetFileByIdQueryHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<FileManagerResponse?> Handle(GetFileByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.GetFileByIdAsync(request.Id, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting file {FileId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
