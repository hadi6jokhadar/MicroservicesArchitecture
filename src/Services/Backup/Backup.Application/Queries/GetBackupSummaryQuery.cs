using Backup.Application.DTOs;
using MediatR;

namespace Backup.Application.Queries;

/// <summary>One row per known backup target, enriched with its most recent backup run (if any).</summary>
public record GetBackupSummaryQuery : IRequest<List<BackupSummaryDto>>;
