using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using MediatR;

namespace FileManager.Application.Handlers.UpdateFile;

public class ToggleArchiveFileCommandHandler : IRequestHandler<ToggleArchiveFileCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;

    public ToggleArchiveFileCommandHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<FileManagerResponse> Handle(ToggleArchiveFileCommand request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.ToggleArchiveStatusAsync(request.Id, cancellationToken);
    }
}
