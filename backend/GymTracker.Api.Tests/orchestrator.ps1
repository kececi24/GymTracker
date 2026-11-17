[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetTestArgs
)

$ErrorActionPreference = "Stop"
# Native exit codes are checked explicitly, including expected readiness failures.
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$runId = [Guid]::NewGuid().ToString("N")
$containerName = "gymtracker-test-pg-$runId"
$sdkContainerName = "gymtracker-test-sdk-$runId"
$nugetVolume = "gymtracker-nuget"
$connectionString = "Host=localhost;Database=gymtracker;Username=gymtracker;Password=gymtracker-ci"

function Invoke-CheckedDocker {
    param([string[]]$DockerArgs)

    & docker @DockerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command '$($DockerArgs[0])' failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Sdk {
    param([string[]]$CommandArgs)

    Invoke-CheckedDocker -DockerArgs (@("exec", $sdkContainerId) + $CommandArgs)
}

function Get-ApplicationTableCount {
    $query = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory';"
    $result = Invoke-CheckedDocker -DockerArgs @(
        "exec", $postgresContainerId, "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
        "-U", "gymtracker", "-d", "gymtracker", "-c", $query
    )
    return [int]($result | Out-String).Trim()
}

$exitCode = 1
$postgresContainerId = $null
$sdkContainerId = $null
try {
    Write-Host "==> Starting disposable PostgreSQL 17 container ($containerName)..." -ForegroundColor Cyan
    $postgresContainerId = Invoke-CheckedDocker -DockerArgs @(
        "run", "-d", "--rm", "--name", $containerName,
        "-e", "POSTGRES_DB=gymtracker", "-e", "POSTGRES_USER=gymtracker",
        "-e", "POSTGRES_PASSWORD=gymtracker-ci", "postgres:17"
    )

    Write-Host "==> Waiting for PostgreSQL to become ready..." -ForegroundColor Cyan
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        docker exec $postgresContainerId pg_isready -h 127.0.0.1 -U gymtracker -d gymtracker | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }

    if (-not $ready) {
        throw "PostgreSQL container did not become ready in time."
    }

    Write-Host "==> Starting .NET 10 SDK container..." -ForegroundColor Cyan
    # Read source without changing the checkout. Keep build outputs in container
    # memory to avoid mixing Windows/Linux assets or locking another local build.
    $sdkContainerId = Invoke-CheckedDocker -DockerArgs @(
        "run", "-d", "--rm", "--name", $sdkContainerName,
        "--network", "container:$postgresContainerId",
        "-v", "${repoRoot}:/work:ro", "-v", "${nugetVolume}:/root/.nuget",
        "--tmpfs", "/work/backend/GymTracker.Api/bin",
        "--tmpfs", "/work/backend/GymTracker.Api/obj",
        "--tmpfs", "/work/backend/GymTracker.Api.Tests/bin",
        "--tmpfs", "/work/backend/GymTracker.Api.Tests/obj",
        "-w", "/work", "-e", "ConnectionStrings__GymTracker=$connectionString",
        "mcr.microsoft.com/dotnet/sdk:10.0", "sleep", "infinity"
    )

    Write-Host "==> Restoring solution dependencies and EF tools..." -ForegroundColor Cyan
    Invoke-Sdk -CommandArgs @("dotnet", "restore", "GymTracker.slnx")
    Invoke-Sdk -CommandArgs @("dotnet", "tool", "restore")

    Write-Host "==> Verifying whitespace formatting (no files are changed)..." -ForegroundColor Cyan
    Invoke-Sdk -CommandArgs @("dotnet", "format", "whitespace", "GymTracker.slnx", "--verify-no-changes", "--no-restore")

    Write-Host "==> Building and checking migration/model consistency..." -ForegroundColor Cyan
    Invoke-Sdk -CommandArgs @("dotnet", "build", "GymTracker.slnx", "--no-restore")
    $efArgs = @("--project", "backend/GymTracker.Api", "--no-build")
    Invoke-Sdk -CommandArgs (@("dotnet", "ef", "migrations", "has-pending-model-changes") + $efArgs)

    Write-Host "==> Applying migrations to the empty disposable database..." -ForegroundColor Cyan
    Invoke-Sdk -CommandArgs (@("dotnet", "ef", "database", "update") + $efArgs)
    $expectedTableCount = Get-ApplicationTableCount
    if ($expectedTableCount -le 0) {
        throw "Migration application did not create any application tables."
    }

    Write-Host "==> Rolling back all migrations (disposable database only)..." -ForegroundColor Cyan
    Invoke-Sdk -CommandArgs (@("dotnet", "ef", "database", "update", "0") + $efArgs)
    if ((Get-ApplicationTableCount) -ne 0) {
        throw "Migration rollback left application tables in the database."
    }

    Write-Host "==> Reapplying migrations..." -ForegroundColor Cyan
    Invoke-Sdk -CommandArgs (@("dotnet", "ef", "database", "update") + $efArgs)
    if ((Get-ApplicationTableCount) -ne $expectedTableCount) {
        throw "Migration reapplication did not restore the application tables."
    }

    Write-Host "==> Running tests against the reapplied schema..." -ForegroundColor Cyan
    Invoke-Sdk -CommandArgs (@("dotnet", "test", "GymTracker.slnx") + $DotnetTestArgs)

    Write-Host "==> All backend validation checks passed." -ForegroundColor Green
    $exitCode = 0
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
}
finally {
    # Only remove containers successfully created by this invocation. The shared
    # NuGet cache is intentionally retained; anonymous database volumes are removed.
    foreach ($ownedContainerId in @($sdkContainerId, $postgresContainerId)) {
        if ($ownedContainerId) {
            Write-Host "==> Cleaning up container $ownedContainerId..." -ForegroundColor DarkGray
            try {
                Invoke-CheckedDocker -DockerArgs @("rm", "--force", "--volumes", $ownedContainerId) | Out-Null
            }
            catch {
                Write-Warning $_.Exception.Message
                $exitCode = 1
            }
        }
    }
}

exit $exitCode
