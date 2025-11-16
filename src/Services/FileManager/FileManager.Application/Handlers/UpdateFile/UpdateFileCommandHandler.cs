using FileManager.Application.Commands;
using FileManager.Application.DTOs;
using FileManager.Application.Interfaces;
using MediatR;

namespace FileManager.Application.Handlers.UpdateFile;

public class UpdateFileCommandHandler : IRequestHandler<UpdateFileCommand, FileManagerResponse>
{
    private readonly IFileManagerService _fileManagerService;

    public UpdateFileCommandHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<FileManagerResponse> Handle(UpdateFileCommand request, CancellationToken cancellationToken)
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
}
