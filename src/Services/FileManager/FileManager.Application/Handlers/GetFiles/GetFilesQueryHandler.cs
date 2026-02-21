using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Application.Queries;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.GetFiles;

public class GetFilesQueryHandler : IRequestHandler<GetFilesQuery, PaginatedList<FileManagerResponse>>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<GetFilesQueryHandler> _logger;

    public GetFilesQueryHandler(
        IFileManagerService fileManagerService,
        ILogger<GetFilesQueryHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<PaginatedList<FileManagerResponse>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.GetFilesAsync(request.Request, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting files list");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
