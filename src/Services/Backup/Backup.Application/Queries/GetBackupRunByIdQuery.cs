using Backup.Application.DTOs;
using MediatR;

namespace Backup.Application.Queries;

public record GetBackupRunByIdQuery(int Id) : IRequest<BackupRunDto>;
