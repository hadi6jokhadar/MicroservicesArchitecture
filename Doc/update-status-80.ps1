$content = Get-Content 'NOTIFICATION_SERVICE_BOTTLENECKS.md' -Encoding UTF8
$content[3] = '**Status:** ⚡ In Progress - 8 of 10 Completed (80%)  '
Set-Content 'NOTIFICATION_SERVICE_BOTTLENECKS.md' -Value $content -Encoding UTF8
Write-Host "Status updated to 80% complete"
