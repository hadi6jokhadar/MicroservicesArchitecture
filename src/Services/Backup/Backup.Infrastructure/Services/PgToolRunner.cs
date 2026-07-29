using System.Diagnostics;
using System.Security.Cryptography;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Backup.Infrastructure.Services;

/// <summary>
/// Shells out to the PostgreSQL client tools (<c>pg_dump</c>/<c>pg_restore</c>). Mirrors
/// FileManager's ffmpeg <see cref="ProcessStartInfo"/> shape (redirected stdout/stderr,
/// <see cref="Process.WaitForExitAsync(CancellationToken)"/>, structured error logging on
/// non-zero exit) — see <c>FileManagerService.ConvertWebMFormFileToMp3Async</c>.
/// </summary>
public class PgToolRunner : IPgToolRunner
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PgToolRunner> _logger;

    public PgToolRunner(IConfiguration configuration, ILogger<PgToolRunner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> DumpAsync(string connectionString, string outputFilePath, CancellationToken ct)
    {
        var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var executable = ResolveExecutablePath(_configuration["Backup:PgDumpPath"], "pg_dump");

        if (string.IsNullOrWhiteSpace(executable))
        {
            _logger.LogError(
                "pg_dump executable was not found. Configure Backup:PgDumpPath or install the PostgreSQL client tools and ensure pg_dump is available on PATH.");
            throw new GeneralException(LocalizationKeys.Exceptions.BackupProcessFailed);
        }

        var arguments = $"-h {csBuilder.Host} -p {csBuilder.Port} -U {csBuilder.Username} -d {csBuilder.Database} " +
                        $"--format=custom --no-password --file=\"{outputFilePath}\"";

        await RunProcessAsync(executable, arguments, csBuilder.Password, "pg_dump", ct);

        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(outputFilePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task RestoreAsync(string connectionString, string inputFilePath, CancellationToken ct)
    {
        var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var executable = ResolveExecutablePath(_configuration["Backup:PgRestorePath"], "pg_restore");

        if (string.IsNullOrWhiteSpace(executable))
        {
            _logger.LogError(
                "pg_restore executable was not found. Configure Backup:PgRestorePath or install the PostgreSQL client tools and ensure pg_restore is available on PATH.");
            throw new GeneralException(LocalizationKeys.Exceptions.BackupProcessFailed);
        }

        var arguments = $"--clean --if-exists --no-owner --no-password -h {csBuilder.Host} -p {csBuilder.Port} " +
                        $"-U {csBuilder.Username} -d {csBuilder.Database} \"{inputFilePath}\"";

        await RunProcessAsync(executable, arguments, csBuilder.Password, "pg_restore", ct);
    }

    private async Task RunProcessAsync(string executable, string arguments, string? password, string toolName, CancellationToken ct)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Pass the password via the environment, never on the command line/args.
        if (!string.IsNullOrEmpty(password))
        {
            processStartInfo.EnvironmentVariables["PGPASSWORD"] = password;
        }

        using var process = new Process { StartInfo = processStartInfo };
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "{ToolName} failed. ExitCode={ExitCode}, StdOut={StdOut}, StdErr={StdErr}",
                toolName, process.ExitCode, stdOut, stdErr);
            throw new GeneralException(LocalizationKeys.Exceptions.BackupProcessFailed);
        }
    }

    private static string? ResolveExecutablePath(string? configuredPath, string executableName)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var trimmed = configuredPath.Trim();

            if (File.Exists(trimmed))
            {
                return trimmed;
            }

            if (string.Equals(trimmed, executableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, $"{executableName}.exe", StringComparison.OrdinalIgnoreCase))
            {
                var fromPath = FindExecutableInPath($"{executableName}.exe") ?? FindExecutableInPath(executableName);
                if (!string.IsNullOrWhiteSpace(fromPath))
                {
                    return fromPath;
                }
            }
        }

        return FindExecutableInPath($"{executableName}.exe") ?? FindExecutableInPath(executableName);
    }

    private static string? FindExecutableInPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var pathEntry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(pathEntry.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }
}
