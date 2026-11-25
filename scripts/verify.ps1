param([string]$Configuration = 'Release', [switch]$SkipIntegration)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet restore Vairas.UserManagement.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Restore or NuGet security audit failed.' }
    dotnet build Vairas.UserManagement.slnx --no-restore -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $results = Join-Path 'Vairas.UserManagement.Tests/TestResults' ([Guid]::NewGuid().ToString('N'))
    dotnet test Vairas.UserManagement.Tests --no-build --no-restore -c $Configuration --filter 'TestCategory=Unit' --settings coverage.runsettings --collect:'XPlat Code Coverage' --results-directory $results --logger 'trx;LogFileName=unit.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }
    # TRX also copies attachments under <machine>/In; read the collector output, once.
    $reports = @(Get-ChildItem -LiteralPath $results -Directory | ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -File -Filter coverage.cobertura.xml
    })
    if ($reports.Count -ne 1) { throw 'Expected exactly one fresh unit coverage report.' }
    [xml]$coverage = Get-Content -Raw -LiteralPath $reports[0].FullName
    $business = @($coverage.coverage.packages.package | Where-Object name -eq 'Vairas.UserManagement.Business')
    if ($business.Count -ne 1) { throw 'The whole Business assembly must be present in coverage.' }
    $rate = [double]::Parse($business[0].'line-rate', [Globalization.CultureInfo]::InvariantCulture)
    if ($rate -lt 0.70) { throw "Business unit-test line coverage below 70%: $rate" }
    Write-Output ('Business unit-test line coverage: {0:P2}' -f $rate)
    Write-Output $reports[0].FullName
    if ($SkipIntegration) {
        Write-Output 'Integration tests NOT RUN: unit-only verification requested.'
    } else {
        dotnet test Vairas.UserManagement.Tests --no-build --no-restore -c $Configuration --filter 'TestCategory!=Unit' --logger 'trx;LogFileName=integration.trx'
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL/HTTP integration tests failed.' }
    }
} finally { Pop-Location }
