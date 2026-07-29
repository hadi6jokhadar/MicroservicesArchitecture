using Backup.Application.DTOs;
using MediatR;

namespace Backup.Application.Queries;

/// <summary>Returns every configured backup target (both scopes), ordered by Scope then DisplayName.</summary>
public record GetBackupTargetsQuery : IRequest<List<BackupTargetDto>>;
