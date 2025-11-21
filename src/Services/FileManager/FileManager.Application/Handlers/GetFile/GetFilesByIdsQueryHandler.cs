using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Application.Queries;
using MediatR;

namespace FileManager.Application.Handlers.GetFile;

public class GetFilesByIdsQueryHandler : IRequestHandler<GetFilesByIdsQuery, List<FileManagerResponse>>
{
    private readonly IFileManagerService _fileManagerService;

    public GetFilesByIdsQueryHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<List<FileManagerResponse>> Handle(GetFilesByIdsQuery request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.GetFilesByIdsAsync(request.Ids, cancellationToken);
    }
}
