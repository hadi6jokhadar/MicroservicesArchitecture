using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Application.Queries;
using MediatR;

namespace FileManager.Application.Handlers.GetFiles;

public class GetFilesQueryHandler : IRequestHandler<GetFilesQuery, PaginatedList<FileManagerResponse>>
{
    private readonly IFileManagerService _fileManagerService;

    public GetFilesQueryHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<PaginatedList<FileManagerResponse>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.GetFilesAsync(request.Request, cancellationToken);
    }
}
