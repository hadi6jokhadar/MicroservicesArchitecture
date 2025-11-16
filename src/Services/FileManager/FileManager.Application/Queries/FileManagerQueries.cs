using FileManager.Application.DTOs;
using MediatR;

namespace FileManager.Application.Queries;

public record GetFileByIdQuery(int Id) : IRequest<FileManagerResponse?>;

public record GetFilesQuery(FileManagerListRequest Request) : IRequest<PaginatedList<FileManagerResponse>>;
