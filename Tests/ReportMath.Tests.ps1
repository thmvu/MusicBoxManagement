#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$offset = [TimeSpan]::FromHours(7)
$day = [DateTimeOffset]::new([DateTime]::new(2026, 9, 1), $offset).ToUniversalTime()
$end = $day.AddDays(1)
$opening = [MusicBoxManagement.Services.ReportMath]::OpeningMinutes($day, $end)
Assert-True ($opening -eq 780) 'A full day should have 780 opening minutes.'
$lunchStart = $day.AddHours(11)
$lunchEnd = $day.AddHours(14)
$used = [MusicBoxManagement.Services.ReportMath]::UsedMinutes($lunchStart, $lunchEnd, $day, $end)
Assert-True ($used -eq 120) 'Lunch break must not count toward usage.'
Assert-True (([MusicBoxManagement.Services.ReportMath]::OpeningMinutes($day.AddHours(12), $day.AddHours(13))) -eq 0) 'Break has no opening minutes.'
Assert-True (([MusicBoxManagement.Services.ReportMath]::OpeningMinutes($day, $day.AddDays(3650))) -eq 2847000) 'Long range opening minutes wrong.'
Write-Output 'PASS Report opening and usage intersection rules'
