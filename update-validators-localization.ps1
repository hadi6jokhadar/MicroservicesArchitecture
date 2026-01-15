# PowerShell script to update all validators with localized field names
# Run this from the MicroservicesArchitecture root directory

$replacements = @{
    '"Email"' = 'L(LocalizationKeys.Fields.Email)'
    '"Password"' = 'L(LocalizationKeys.Fields.Password)'
    '"First name"' = 'L(LocalizationKeys.Fields.FirstName)'
    '"FirstName"' = 'L(LocalizationKeys.Fields.FirstName)'
    '"Last name"' = 'L(LocalizationKeys.Fields.LastName)'
    '"LastName"' = 'L(LocalizationKeys.Fields.LastName)'
    '"Phone number"' = 'L(LocalizationKeys.Fields.PhoneNumber)'
    '"User ID"' = 'L(LocalizationKeys.Fields.UserId)'
    '"UserId"' = 'L(LocalizationKeys.Fields.UserId)'
    '"Role ID"' = 'L(LocalizationKeys.Fields.RoleId)'
    '"Claim ID"' = 'L(LocalizationKeys.Fields.ClaimId)'
    '"Role name"' = 'L(LocalizationKeys.Fields.RoleName)'
    '"Claim name"' = 'L(LocalizationKeys.Fields.ClaimName)'
    '"Claim type"' = 'L(LocalizationKeys.Fields.ClaimType)'
    '"Claim value"' = 'L(LocalizationKeys.Fields.ClaimValue)'
    '"Description"' = 'L(LocalizationKeys.Fields.Description)'
    '"Roles"' = 'L(LocalizationKeys.Fields.Roles)'
    '"Claims"' = 'L(LocalizationKeys.Fields.Claims)'
    '"File"' = 'L(LocalizationKeys.Fields.File)'
    '"Group"' = 'L(LocalizationKeys.Fields.Group)'
    '"Sort column"' = 'L(LocalizationKeys.Fields.SortColumn)'
    '"Tenant ID"' = 'L(LocalizationKeys.Fields.TenantId)'
    '"Tenant name"' = 'L(LocalizationKeys.Fields.TenantName)'
    '"Start date"' = 'L(LocalizationKeys.Fields.StartDate)'
    '"Expire date"' = 'L(LocalizationKeys.Fields.ExpireDate)'
    '"Configuration data"' = 'L(LocalizationKeys.Fields.ConfigurationData)'
    '"Title"' = 'L(LocalizationKeys.Fields.Title)'
    '"Message"' = 'L(LocalizationKeys.Fields.Message)'
    '"DeliveryType"' = 'L(LocalizationKeys.Fields.DeliveryType)'
    '"Delivery type"' = 'L(LocalizationKeys.Fields.DeliveryType)'
    '"Priority"' = 'L(LocalizationKeys.Fields.Priority)'
    '"NotificationId"' = 'L(LocalizationKeys.Fields.NotificationId)'
    '"Notification ID"' = 'L(LocalizationKeys.Fields.NotificationId)'
    '"QueueItemId"' = 'L(LocalizationKeys.Fields.QueueItemId)'
    '"Queue item ID"' = 'L(LocalizationKeys.Fields.QueueItemId)'
    '"Skip"' = 'L(LocalizationKeys.Fields.Skip)'
    '"Take"' = 'L(LocalizationKeys.Fields.Take)'
    '"Refresh token"' = 'L(LocalizationKeys.Fields.RefreshToken)'
    '"Verification code"' = 'L(LocalizationKeys.Fields.VerificationCode)'
}

# Find all command and validator files
$files = Get-ChildItem -Path "src\Services" -Include "*Command*.cs","*Validator*.cs","*Query*.cs" -Recurse

Write-Host "Found $($files.Count) files to process..." -ForegroundColor Yellow

$updatedCount = 0

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    
    foreach ($key in $replacements.Keys) {
        $pattern = [regex]::Escape($key)
        if ($content -match $pattern) {
            $content = $content -replace $pattern, $replacements[$key]
        }
    }
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "✓ Updated: $($file.FullName.Replace((Get-Location).Path, ''))" -ForegroundColor Green
        $updatedCount++
    }
}

Write-Host "`nCompleted! Updated $updatedCount files." -ForegroundColor Cyan
Write-Host "Please review the changes and test the validators." -ForegroundColor Yellow
