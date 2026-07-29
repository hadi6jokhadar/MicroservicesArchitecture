using System.Text.Json.Serialization;
using Backup.Application.DTOs;
using Backup.Domain.Enums;
using MediatR;

namespace Backup.Application.Commands;

/// <summary>
/// Manually triggers a backup run for a service's global database or a single tenant's database.
/// If no matching <see cref="Backup.Domain.Entities.BackupTargetEntity"/> exists yet, one is
/// created on the fly (enabled by default).
/// </summary>
/// <remarks>
/// <see cref="Scope"/> needs <see cref="JsonStringEnumConverter"/> explicitly — no global
/// enum-as-string converter is registered anywhere in this platform (every other service maps
/// enums to strings manually on the DTO/output side via <c>.ToString()</c>), so without this
/// attribute System.Text.Json expects the request body to send the enum as its underlying int,
/// while every frontend caller (and this codebase's own DTOs) sends/renders enum names as
/// strings, e.g. <c>"scope": "GlobalService"</c> — the request would otherwise fail to bind with
/// a 400 (<c>JsonException: The JSON value could not be converted to ... Path: $.scope</c>).
/// </remarks>
public record TriggerBackupCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] BackupScope Scope,
    string? ServiceName,
    string? TenantId) : IRequest<BackupRunDto>;
