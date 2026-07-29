using Backup.Application.DTOs;
using MediatR;

namespace Backup.Application.Commands;

/// <summary>
/// Triggers a restore from a previously completed backup run. <see cref="Confirm"/> must be
/// explicitly set to <c>true</c> — this is a destructive operation against the target database.
/// </summary>
public record TriggerRestoreCommand(int BackupRunId, bool Confirm, string? TargetConnectionOverride) : IRequest<RestoreRunDto>;
