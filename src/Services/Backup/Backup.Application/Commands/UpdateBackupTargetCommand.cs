using Backup.Application.DTOs;
using MediatR;

namespace Backup.Application.Commands;

/// <summary>
/// Updates the enable/disable flag and/or retention override on an existing backup target.
/// Only non-null fields are applied — omitted fields are left unchanged.
/// </summary>
public record UpdateBackupTargetCommand(int Id, bool? IsEnabled, int? RetentionDays) : IRequest<BackupTargetDto>;
