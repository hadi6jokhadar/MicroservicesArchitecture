using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Application.Queries;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileManager.Application.Handlers.GetFile;

public class GetFilesByIdsQueryHandler : IRequestHandler<GetFilesByIdsQuery, List<FileManagerResponse>>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger<GetFilesByIdsQueryHandler> _logger;

    public GetFilesByIdsQueryHandler(
        IFileManagerService fileManagerService,
        ILogger<GetFilesByIdsQueryHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _logger = logger;
    }

    public async Task<List<FileManagerResponse>> Handle(GetFilesByIdsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileManagerService.GetFilesByIdsAsync(request.Ids, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting files by ids");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
