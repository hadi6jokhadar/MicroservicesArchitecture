using FileManager.Application.Commands;
using FileManager.Application.Interfaces;
using MediatR;

namespace FileManager.Application.Handlers.DeleteFile;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, bool>
{
    private readonly IFileManagerService _fileManagerService;

    public DeleteFileCommandHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<bool> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.DeleteFileAsync(request.Id, cancellationToken);
    }
}
