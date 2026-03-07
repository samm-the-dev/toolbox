<#
.SYNOPSIS
Deploy the shared OAuth token exchange Cloud Function with project-specific config.

.DESCRIPTION
Builds and deploys the token exchange function from the submodule source
using google-cloud-auth.config.json from the consuming project root.
Run this script from the consuming project root directory.

See .toolbox/ai-context/google-cloud-auth.md.

Config file fields (google-cloud-auth.config.json):
  functionName  (required) - GCP function name
  entryPoint    (required) - exported function name
  secrets       (required) - --set-secrets value
  envVars       (optional) - --set-env-vars value (e.g., ALLOWED_ORIGINS=https://example.com)
  region        (optional) - default: us-central1
  runtime       (optional) - default: nodejs22

.EXAMPLE
# From the consuming project root:
powershell -ExecutionPolicy Bypass -File .toolbox/google-cloud-auth/deploy.ps1
#>

$ErrorActionPreference = 'Stop'

$ConfigFile = 'google-cloud-auth.config.json'
$SourceDir = Join-Path $PSScriptRoot 'function'

if (-not (Test-Path $ConfigFile)) {
    Write-Error "$ConfigFile not found in $(Get-Location). Run this script from the consuming project root."
    exit 1
}

if (-not (Test-Path $SourceDir)) {
    Write-Error "Source directory not found: $SourceDir"
    exit 1
}

$config = Get-Content $ConfigFile -Raw | ConvertFrom-Json

$functionName = $config.functionName
$entryPoint = $config.entryPoint
$secrets = $config.secrets
$envVars = $config.envVars
$region = if ($config.region) { $config.region } else { 'us-central1' }
$runtime = if ($config.runtime) { $config.runtime } else { 'nodejs22' }

if (-not $functionName -or -not $entryPoint -or -not $secrets) {
    Write-Error "$ConfigFile must specify functionName, entryPoint, and secrets"
    exit 1
}

Push-Location $SourceDir

try {
    Write-Host "Deploying $functionName (entry: $entryPoint) to $region..."

    npm install --silent
    if ($LASTEXITCODE -ne 0) {
        Write-Error 'npm install failed'
        exit $LASTEXITCODE
    }

    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Error 'Build failed'
        exit $LASTEXITCODE
    }

    $deployArgs = @(
        'functions', 'deploy', $functionName,
        '--gen2',
        "--runtime=$runtime",
        '--trigger-http',
        '--allow-unauthenticated',
        "--entry-point=$entryPoint",
        '--source=.',
        "--region=$region",
        "--set-secrets=$secrets"
    )
    if ($envVars) {
        $deployArgs += "--set-env-vars=$envVars"
    }
    & gcloud @deployArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Error 'Deploy failed'
        exit $LASTEXITCODE
    }

    Write-Host "Deployed $functionName successfully."
}
finally {
    Pop-Location
}
