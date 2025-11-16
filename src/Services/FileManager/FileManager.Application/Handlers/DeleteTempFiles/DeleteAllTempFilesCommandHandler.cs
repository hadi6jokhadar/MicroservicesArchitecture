using FileManager.Application.Commands;
using FileManager.Application.Interfaces;
using MediatR;

namespace FileManager.Application.Handlers.DeleteTempFiles;

public class DeleteAllTempFilesCommandHandler : IRequestHandler<DeleteAllTempFilesCommand, int>
{
    private readonly IFileManagerService _fileManagerService;

    public DeleteAllTempFilesCommandHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<int> Handle(DeleteAllTempFilesCommand request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.DeleteAllTempFilesAsync(cancellationToken);
    }
}
