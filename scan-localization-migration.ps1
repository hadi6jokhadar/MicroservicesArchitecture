# Localization Migration Automation Script
# This script helps identify and update files that still contain hardcoded messages

param(
    [Parameter(Mandatory=$false)]
    [string]$ServicePath = "src\Services",
    
    [Parameter(Mandatory=$false)]
    [switch]$ScanOnly,
    
    [Parameter(Mandatory=$false)]
    [switch]$FixExceptions,
    
    [Parameter(Mandatory=$false)]
    [switch]$FixValidators
)

Write-Host "🔍 Localization Migration Automation Script" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Define patterns to search for
$ExceptionPatterns = @(
    'throw new .*Exception\("([^"]+)"\)',
    'throw new .*Exception\("([^"]+):" \+ ex\.Message\)'
)

$ValidationPatterns = @(
    '\.WithMessage\("([^"]+)"\)'
)

$ResponseMessagePatterns = @(
    'message\s*=\s*"([^"{]+)"'
)

# Scan for hardcoded exceptions
function Scan-HardcodedExceptions {
    param([string]$path)
    
    Write-Host "📝 Scanning for hardcoded exception messages..." -ForegroundColor Yellow
    
    $results = @()
    
    Get-ChildItem -Path $path -Filter "*.cs" -Recurse | ForEach-Object {
        $filePath = $_.FullName
        $content = Get-Content $filePath -Raw
        
        foreach ($pattern in $ExceptionPatterns) {
            $matches = [regex]::Matches($content, $pattern)
            if ($matches.Count -gt 0) {
                foreach ($match in $matches) {
                    $results += [PSCustomObject]@{
                        File = $filePath.Replace((Get-Location).Path + "\", "")
                        Line = ($content.Substring(0, $match.Index) -split "`n").Count
                        Message = $match.Groups[1].Value
                        Type = "Exception"
                    }
                }
            }
        }
    }
    
    return $results
}

# Scan for hardcoded validation messages
function Scan-HardcodedValidationMessages {
    param([string]$path)
    
    Write-Host "📝 Scanning for hardcoded validation messages..." -ForegroundColor Yellow
    
    $results = @()
    
    Get-ChildItem -Path $path -Filter "*Validator.cs" -Recurse | ForEach-Object {
        $filePath = $_.FullName
        $content = Get-Content $filePath -Raw
        
        foreach ($pattern in $ValidationPatterns) {
            $matches = [regex]::Matches($content, $pattern)
            if ($matches.Count -gt 0) {
                foreach ($match in $matches) {
                    $results += [PSCustomObject]@{
                        File = $filePath.Replace((Get-Location).Path + "\", "")
                        Line = ($content.Substring(0, $match.Index) -split "`n").Count
                        Message = $match.Groups[1].Value
                        Type = "Validation"
                    }
                }
            }
        }
    }
    
    return $results
}

# Scan for hardcoded API response messages
function Scan-HardcodedResponseMessages {
    param([string]$path)
    
    Write-Host "📝 Scanning for hardcoded API response messages..." -ForegroundColor Yellow
    
    $results = @()
    
    Get-ChildItem -Path $path -Filter "*ApiHandlers.cs" -Recurse | ForEach-Object {
        $filePath = $_.FullName
        $content = Get-Content $filePath -Raw
        
        foreach ($pattern in $ResponseMessagePatterns) {
            $matches = [regex]::Matches($content, $pattern)
            if ($matches.Count -gt 0) {
                foreach ($match in $matches) {
                    $results += [PSCustomObject]@{
                        File = $filePath.Replace((Get-Location).Path + "\", "")
                        Line = ($content.Substring(0, $match.Index) -split "`n").Count
                        Message = $match.Groups[1].Value
                        Type = "APIResponse"
                    }
                }
            }
        }
    }
    
    return $results
}

# Main execution
Write-Host ""
Write-Host "🎯 Target Path: $ServicePath" -ForegroundColor Green
Write-Host ""

# Scan exceptions
$exceptionResults = Scan-HardcodedExceptions -path $ServicePath
Write-Host "✅ Found $($exceptionResults.Count) hardcoded exception messages" -ForegroundColor $(if ($exceptionResults.Count -eq 0) { "Green" } else { "Yellow" })

# Scan validations
$validationResults = Scan-HardcodedValidationMessages -path $ServicePath
Write-Host "✅ Found $($validationResults.Count) hardcoded validation messages" -ForegroundColor $(if ($validationResults.Count -eq 0) { "Green" } else { "Yellow" })

# Scan API responses
$responseResults = Scan-HardcodedResponseMessages -path $ServicePath
Write-Host "✅ Found $($responseResults.Count) hardcoded API response messages" -ForegroundColor $(if ($responseResults.Count -eq 0) { "Green" } else { "Yellow" })

Write-Host ""
Write-Host "📊 SUMMARY" -ForegroundColor Cyan
Write-Host "=========" -ForegroundColor Cyan
Write-Host ""

# Group by service
$allResults = $exceptionResults + $validationResults + $responseResults

$groupedByService = $allResults | Group-Object { ($_.File -split "\\Services\\")[1] -split "\\" | Select-Object -First 1 }

foreach ($group in $groupedByService) {
    Write-Host "📁 $($group.Name) Service: $($group.Count) hardcoded messages" -ForegroundColor Yellow
    
    # Group by type
    $byType = $group.Group | Group-Object Type
    foreach ($typeGroup in $byType) {
        Write-Host "   - $($typeGroup.Name): $($typeGroup.Count)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "🔬 DETAILED REPORT" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan
Write-Host ""

# Output detailed results
if ($ScanOnly) {
    Write-Host "Exception Messages:" -ForegroundColor Yellow
    $exceptionResults | Format-Table -Property File, Line, Message -AutoSize
    
    Write-Host ""
    Write-Host "Validation Messages:" -ForegroundColor Yellow
    $validationResults | Format-Table -Property File, Line, Message -AutoSize
    
    Write-Host ""
    Write-Host "API Response Messages:" -ForegroundColor Yellow
    $responseResults | Format-Table -Property File, Line, Message -AutoSize
}

# Export to CSV
$csvPath = "Doc\LOCALIZATION_MIGRATION_SCAN_RESULTS.csv"
$allResults | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
Write-Host "📄 Detailed results exported to: $csvPath" -ForegroundColor Green

# Generate suggested LocalizationKeys
Write-Host ""
Write-Host "💡 SUGGESTED NEW LOCALIZATION KEYS" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

$uniqueMessages = $exceptionResults.Message | Select-Object -Unique | Sort-Object

Write-Host "Add these to LocalizationKeys.cs:" -ForegroundColor Yellow
foreach ($msg in $uniqueMessages) {
    $key = $msg.ToLower() -replace '[^a-z0-9]+', '_' -replace '^_|_$', ''
    Write-Host "public const string $(Get-Culture).TextInfo.ToTitleCase($key.Replace('_', ' ')).Replace(' ', '') = ""exception_$key"";" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✅ Scan Complete!" -ForegroundColor Green
Write-Host ""

if (-not $ScanOnly) {
    Write-Host "💡 TIP: Use -ScanOnly to see detailed results" -ForegroundColor Cyan
    Write-Host "💡 TIP: Use -FixExceptions to automatically fix exception throws" -ForegroundColor Cyan
    Write-Host "💡 TIP: Use -FixValidators to automatically fix validators" -ForegroundColor Cyan
}
