[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetTestArgs
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$containerName = "gymtracker-test-pg"
$nugetVolume = "gymtracker-nuget"
$connectionString = "Host=localhost;Database=gymtracker;Username=gymtracker;Password=gymtracker-ci"

Write-Host "==> Starting disposable PostgreSQL 17 container ($containerName)..." -ForegroundColor Cyan
# docker rm -f $containerName 2>$null | Out-Null

$exitCode = 1
try {
    docker run -d `
        --name $containerName `
        -e POSTGRES_DB=gymtracker `
        -e POSTGRES_USER=gymtracker `
        -e POSTGRES_PASSWORD=gymtracker-ci `
        postgres:17 | Out-Null

    Write-Host "==> Waiting for PostgreSQL to become ready..." -ForegroundColor Cyan
    $ready = $false
    for ($i = 0; $i -lt 20; $i++) {
        docker exec $containerName pg_isready -h 127.0.0.1 -U gymtracker -d gymtracker 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }

    if (-not $ready) {
        throw "PostgreSQL container did not become ready in time."
    }

    Write-Host "==> Running GymTracker.Api.Tests in .NET 10 SDK container..." -ForegroundColor Cyan
    docker run --rm `
        --network "container:$containerName" `
        -v "${repoRoot}:/work" `
        -v "${nugetVolume}:/root/.nuget" `
        -w /work `
        -e "ConnectionStrings__GymTracker=$connectionString" `
        mcr.microsoft.com/dotnet/sdk:10.0 `
        dotnet test GymTracker.slnx @DotnetTestArgs

    $exitCode = $LASTEXITCODE
}
finally {
    Write-Host "==> Cleaning up PostgreSQL container ($containerName)..." -ForegroundColor DarkGray
    docker rm -f $containerName 2>$null | Out-Null
}

exit $exitCode
