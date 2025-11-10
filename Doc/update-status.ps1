$content = Get-Content 'NOTIFICATION_SERVICE_BOTTLENECKS.md' -Encoding UTF8
$content[3] = '**Status:** ⚡ In Progress - 5 of 10 Completed (50%)  '
Set-Content 'NOTIFICATION_SERVICE_BOTTLENECKS.md' -Value $content -Encoding UTF8
Write-Host "Status updated to 50% complete"
