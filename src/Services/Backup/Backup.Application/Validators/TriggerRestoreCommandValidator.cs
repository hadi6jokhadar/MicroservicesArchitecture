using System.Text.RegularExpressions;
using Backup.Application.Commands;
using FluentValidation;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Validation;

namespace Backup.Application.Validators;

public class TriggerRestoreCommandValidator : LocalizedValidator<TriggerRestoreCommand>
{
    // Defense in depth alongside PgToolRunner's ArgumentList switch: even though arguments are no
    // longer shell-interpolated, TargetConnectionOverride is caller-supplied and reaches
    // NpgsqlConnectionStringBuilder unchecked otherwise. Only the connection-forming components
    // (host/port/user/database) are restricted — the password component is intentionally left
    // alone since it never touches process arguments (PGPASSWORD env var only).
    private static readonly Regex SafeComponentPattern = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);
    private static readonly string[] RestrictedKeys =
    [
        "host", "server", "port", "username", "user id", "userid", "uid", "database", "initial catalog"
    ];

    public TriggerRestoreCommandValidator(ILocalizationService localizationService) : base(localizationService)
    {
        RuleFor(x => x.Confirm)
            .Equal(true)
            .WithMessage(L(LocalizationKeys.Validation.ConfirmationRequired));

        RuleFor(x => x.TargetConnectionOverride)
            .Must(HaveSafeConnectionStringComponents)
            .WithMessage(L(LocalizationKeys.Validation.InvalidFormat, L(LocalizationKeys.Fields.TargetConnectionOverride)))
            .When(x => !string.IsNullOrWhiteSpace(x.TargetConnectionOverride));
    }

    private static bool HaveSafeConnectionStringComponents(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return true;
        }

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim().ToLowerInvariant();
            var value = segment[(separatorIndex + 1)..].Trim();

            if (Array.IndexOf(RestrictedKeys, key) < 0)
            {
                continue;
            }

            if (value.Length == 0 || !SafeComponentPattern.IsMatch(value))
            {
                return false;
            }
        }

        return true;
    }
}
