using FileManager.Application.Commands;
using FileManager.Application.Interfaces;
using MediatR;

namespace FileManager.Application.Handlers.DeleteTempFiles;

public class DeleteOldTempFilesCommandHandler : IRequestHandler<DeleteOldTempFilesCommand, int>
{
    private readonly IFileManagerService _fileManagerService;

    public DeleteOldTempFilesCommandHandler(IFileManagerService fileManagerService)
    {
        _fileManagerService = fileManagerService;
    }

    public async Task<int> Handle(DeleteOldTempFilesCommand request, CancellationToken cancellationToken)
    {
        return await _fileManagerService.DeleteOldTempFilesAsync(request.OlderThanDays, cancellationToken);
    }
}
