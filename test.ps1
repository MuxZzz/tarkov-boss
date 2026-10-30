param([switch]$IncludePrivateLogs)
$ErrorActionPreference = 'Stop'
$previousLogRoot = [Environment]::GetEnvironmentVariable('TARKOV_BOSS_TEST_LOG_ROOT')
try {
    if ($IncludePrivateLogs) {
        $privateLogRoot = Join-Path $PSScriptRoot 'work\test-logs'
        if (-not (Test-Path -LiteralPath (Join-Path $privateLogRoot 'Logs'))) {
            throw 'work/test-logs/Logs에 보관된 원본 로그가 없습니다. 기본 테스트는 -IncludePrivateLogs 없이 실행하세요.'
        }
        $env:TARKOV_BOSS_TEST_LOG_ROOT = $privateLogRoot
    }
    & dotnet test (Join-Path $PSScriptRoot 'tests\BossMonitor.Tests\BossMonitor.Tests.csproj') -c Release --nologo --logger trx --results-directory (Join-Path $PSScriptRoot 'work\test-results')
    if ($LASTEXITCODE -ne 0) { throw "회귀 테스트 실패 (종료 코드 $LASTEXITCODE)" }
}
finally {
    [Environment]::SetEnvironmentVariable('TARKOV_BOSS_TEST_LOG_ROOT', $previousLogRoot)
}
