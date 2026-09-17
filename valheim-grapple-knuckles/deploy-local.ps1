# Builds GrappleKnuckles and copies it into the r2modman "GrappleKnucklesDev" profile
# so it shows up in r2modman's mod list (toggleable) without polluting the main Default profile.
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$profileDir = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\GrappleKnucklesDev"
$pluginDir = "$profileDir\BepInEx\plugins\GrappleKnuckles"

dotnet build "$repoRoot\GrappleKnuckles\GrappleKnuckles.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item "$repoRoot\GrappleKnuckles\bin\Release\GrappleKnuckles.dll" $pluginDir -Force
Copy-Item "$repoRoot\GrappleKnuckles\manifest.json" $pluginDir -Force
Copy-Item "$repoRoot\README.md" $pluginDir -Force

Write-Output "Deployed to $pluginDir"
Write-Output "Launch Valheim through r2modman using the 'GrappleKnucklesDev' profile."
