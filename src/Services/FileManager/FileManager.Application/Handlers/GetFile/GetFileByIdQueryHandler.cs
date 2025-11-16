using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using FileManager.Application.Queries;
using MediatR;

namespace FileManager.Application.Handlers.GetFile;

public class GetFileByIdQueryHandler : IRequestHandler<GetFileByIdQuery, FileManagerResponse?>
{
    private readonly IFileManagerService _fileManagerService;

    public GetFileByIdQueryHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<FileManagerResponse?> Handle(GetFileByIdQuery request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.GetFileByIdAsync(request.Id, cancellationToken);
    }
}
