# CORS Testing Script for Multi-Tenant Architecture
# This script tests CORS behavior with and without tenant context

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CORS Testing Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$identityServiceUrl = "https://localhost:5101"
$tenantServiceUrl = "http://localhost:5002"

# Test data
$defaultOrigin = "http://localhost:4200"
$tenantOrigin = "https://tenant-app.com"
$invalidOrigin = "https://malicious-site.com"

# Function to test CORS
function Test-Cors {
    param(
        [string]$Url,
        [string]$Origin,
        [string]$TenantId = $null,
        [string]$TestName
    )
    
    Write-Host "`n--- $TestName ---" -ForegroundColor Yellow
    Write-Host "URL: $Url" -ForegroundColor Gray
    Write-Host "Origin: $Origin" -ForegroundColor Gray
    if ($TenantId) {
        Write-Host "Tenant: $TenantId" -ForegroundColor Gray
    } else {
        Write-Host "Tenant: (none)" -ForegroundColor Gray
    }
    
    try {
        $headers = @{
            "Origin" = $Origin
            "Content-Type" = "application/json"
        }
        
        if ($TenantId) {
            $headers["x-tenant-id"] = $TenantId
        }
        
        # Make OPTIONS request (preflight)
        Write-Host "`nPreflight Request (OPTIONS):" -ForegroundColor Cyan
        $response = Invoke-WebRequest -Uri $Url -Method OPTIONS -Headers $headers -SkipCertificateCheck -ErrorAction Stop
        
        Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
        
        # Check CORS headers
        $corsHeader = $response.Headers["Access-Control-Allow-Origin"]
        $credsHeader = $response.Headers["Access-Control-Allow-Credentials"]
        
        if ($corsHeader) {
            Write-Host "✓ Access-Control-Allow-Origin: $corsHeader" -ForegroundColor Green
        } else {
            Write-Host "✗ No Access-Control-Allow-Origin header" -ForegroundColor Red
        }
        
        if ($credsHeader) {
            Write-Host "✓ Access-Control-Allow-Credentials: $credsHeader" -ForegroundColor Green
        } else {
            Write-Host "✗ No Access-Control-Allow-Credentials header" -ForegroundColor Red
        }
        
        # Make actual POST request
        Write-Host "`nActual Request (POST):" -ForegroundColor Cyan
        $body = @{
            email = "test@example.com"
            password = "Test123!"
        } | ConvertTo-Json
        
        $response = Invoke-WebRequest -Uri $Url -Method POST -Headers $headers -Body $body -SkipCertificateCheck -ErrorAction Stop
        
        Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
        
        $corsHeader = $response.Headers["Access-Control-Allow-Origin"]
        if ($corsHeader) {
            Write-Host "✓ Access-Control-Allow-Origin: $corsHeader" -ForegroundColor Green
        } else {
            Write-Host "✗ No Access-Control-Allow-Origin header" -ForegroundColor Red
        }
        
        Write-Host "Result: PASS ✓" -ForegroundColor Green
        
    } catch {
        Write-Host "Status: $($_.Exception.Response.StatusCode.Value__)" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        
        # Still check for CORS headers in error response
        $corsHeader = $_.Exception.Response.Headers["Access-Control-Allow-Origin"]
        if ($corsHeader) {
            Write-Host "Access-Control-Allow-Origin: $corsHeader" -ForegroundColor Yellow
        }
        
        Write-Host "Result: FAIL ✗" -ForegroundColor Red
    }
}

# Function to create test tenant
function Create-TestTenant {
    param(
        [string]$TenantId,
        [string]$TenantName,
        [string[]]$AllowedOrigins
    )
    
    Write-Host "`n--- Creating Test Tenant: $TenantId ---" -ForegroundColor Yellow
    
    $tenantConfig = @{
        Cors = @{
            AllowedOrigins = $AllowedOrigins
        }
        Database = @{
            Provider = "PostgreSql"
            ConnectionString = "Host=localhost;Port=5432;Database=tenant_$TenantId;Username=postgres;Password=CHANGE_ME_DB_PASSWORD;"
        }
        Jwt = @{
            Secret = "test-tenant-secret-key-minimum-32-characters-long"
            Issuer = "TenantIssuer"
            Audience = "TenantApp"
            AccessTokenExpirationMinutes = 60
        }
    } | ConvertTo-Json -Depth 10
    
    $body = @{
        tenantId = $TenantId
        tenantName = $TenantName
        userId = 1
        startDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
        expireDate = (Get-Date).AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
        data = $tenantConfig
    } | ConvertTo-Json
    
    try {
        # Note: You'll need a valid admin token for this
        Write-Host "This requires admin authentication. Please create tenant manually via Swagger UI." -ForegroundColor Yellow
        Write-Host "Tenant Configuration:" -ForegroundColor Cyan
        Write-Host $tenantConfig -ForegroundColor Gray
    } catch {
        Write-Host "Error creating tenant: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Main Testing Flow
Write-Host "`n=== CORS Testing Scenarios ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Prerequisites:" -ForegroundColor Yellow
Write-Host "1. Identity Service running on $identityServiceUrl" -ForegroundColor Gray
Write-Host "2. Tenant Service running on $tenantServiceUrl" -ForegroundColor Gray
Write-Host "3. MultiTenancy:Enabled = true in Identity Service config" -ForegroundColor Gray
Write-Host ""

# Test 1: Default CORS (no tenant)
Test-Cors -Url "$identityServiceUrl/api/auth/login" `
          -Origin $defaultOrigin `
          -TestName "Test 1: Default Origin (No Tenant)"

# Test 2: Invalid origin (no tenant)
Test-Cors -Url "$identityServiceUrl/api/auth/login" `
          -Origin $invalidOrigin `
          -TestName "Test 2: Invalid Origin (No Tenant)"

# Test 3: With tenant (requires tenant to be created first)
Write-Host "`n`n=== Tenant-Specific Tests ===" -ForegroundColor Cyan
Write-Host "Note: These tests require a tenant named 'test-tenant' with CORS origins configured" -ForegroundColor Yellow
Write-Host ""

$testTenantId = "test-tenant"
$response = Read-Host "Do you have a test tenant created? (y/n)"

if ($response -eq "y") {
    # Test 4: Tenant-specific origin
    Test-Cors -Url "$identityServiceUrl/api/auth/login" `
              -Origin $tenantOrigin `
              -TenantId $testTenantId `
              -TestName "Test 3: Tenant-Specific Origin (With Tenant)"
    
    # Test 5: Default origin with tenant (should fail if tenant has specific CORS)
    Test-Cors -Url "$identityServiceUrl/api/auth/login" `
              -Origin $defaultOrigin `
              -TenantId $testTenantId `
              -TestName "Test 4: Default Origin (With Tenant)"
    
    # Test 6: Invalid origin with tenant
    Test-Cors -Url "$identityServiceUrl/api/auth/login" `
              -Origin $invalidOrigin `
              -TenantId $testTenantId `
              -TestName "Test 5: Invalid Origin (With Tenant)"
} else {
    Write-Host "`nTo create a test tenant:" -ForegroundColor Cyan
    Write-Host "1. Run Identity Service and Tenant Service" -ForegroundColor Gray
    Write-Host "2. Open Swagger UI: $tenantServiceUrl/swagger" -ForegroundColor Gray
    Write-Host "3. Get admin token from Identity Service" -ForegroundColor Gray
    Write-Host "4. Use POST /api/admin/tenant endpoint with this data:" -ForegroundColor Gray
    Write-Host ""
    
    $sampleConfig = @{
        tenantId = "test-tenant"
        tenantName = "Test Tenant"
        userId = 1
        startDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
        expireDate = (Get-Date).AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
        data = "{`"Cors`":{`"AllowedOrigins`":[`"https://tenant-app.com`",`"https://admin.tenant-app.com`"]},`"Database`":{`"Provider`":`"PostgreSql`",`"ConnectionString`":`"Host=localhost;Port=5432;Database=tenant_test;Username=postgres;Password=CHANGE_ME_DB_PASSWORD;`"},`"Jwt`":{`"Secret`":`"test-tenant-secret-key-minimum-32-characters-long`",`"Issuer`":`"TenantIssuer`",`"Audience`":`"TenantApp`",`"AccessTokenExpirationMinutes`":60}}"
    } | ConvertTo-Json -Depth 10
    
    Write-Host $sampleConfig -ForegroundColor Gray
}

Write-Host "`n`n========================================" -ForegroundColor Cyan
Write-Host "  Testing Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
