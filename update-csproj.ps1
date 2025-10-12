# Get all .csproj files
$projects = Get-ChildItem -Path . -Recurse -Filter *.csproj

foreach ($project in $projects) {
    Write-Host "Processing: $($project.FullName)" -ForegroundColor Cyan
    
    $content = Get-Content $project.FullName -Raw
    
    # Remove Version attributes from PackageReference
    $content = $content -replace '<PackageReference Include="([^"]+)" Version="[^"]+" />', '<PackageReference Include="$1" />'
    $content = $content -replace '<PackageReference Include="([^"]+)" Version="[^"]+">',  '<PackageReference Include="$1">'
    
    # Save the file
    Set-Content -Path $project.FullName -Value $content
    
    Write-Host "✅ Updated: $($project.Name)" -ForegroundColor Green
}

Write-Host "`n✅ All projects updated!" -ForegroundColor Green
Write-Host "Now run: dotnet restore" -ForegroundColor Yellow


# Run this script using: F5 to update all .csproj files in the current directory and its subdirectories.