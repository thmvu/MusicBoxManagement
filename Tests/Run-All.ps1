#requires -PSEdition Desktop
$ErrorActionPreference = 'Stop'
$tests = Get-ChildItem $PSScriptRoot -Filter '*.ps1' |
    Where-Object { $_.Name -ne 'Run-All.ps1' } |
    Sort-Object Name
$failed = @()
foreach ($test in $tests)
{
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $test.FullName
    if ($LASTEXITCODE -eq 0)
    {
        Write-Output "PASS $($test.Name)"
    }
    else
    {
        $failed += $test.Name
        Write-Output "FAIL $($test.Name)"
    }
}
if ($failed.Count -gt 0)
{
    throw ("Các test thất bại: " + ($failed -join ', '))
}
Write-Output "PASS $($tests.Count)/$($tests.Count) test files"
