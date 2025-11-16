using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using MediatR;

namespace FileManager.Application.Handlers.SaveFile;

public class SaveFileCommandHandler : IRequestHandler<SaveFileCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;

    public SaveFileCommandHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<FileManagerResponse> Handle(SaveFileCommand request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.SaveFileAsync(
            request.File,
            request.Group,
            request.UserId,
            cancellationToken);
    }
}
