param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path

function Assert-Result($name, $start, $minutes, $now, $valid) {
    $result = [MusicBoxManagement.Services.BookingTimeRules]::Validate($start, $minutes, $now)
    if ($result.IsValid -ne $valid) { throw "$name : expected $valid, got $($result.IsValid); $($result.Error)" }
    if ($valid -and $result.EndTimeUtc -ne $start.AddMinutes($minutes)) { throw "$name : wrong end time" }
    Write-Output "PASS $name"
}

$now = [DateTimeOffset]::Parse('2026-09-25T01:00:00+00:00')
Assert-Result 'Ca sang 09:00-12:00' ([DateTimeOffset]::Parse('2026-09-25T02:00:00+00:00')) 180 $now $true
Assert-Result 'Ca toi 20:00-23:00' ([DateTimeOffset]::Parse('2026-09-25T13:00:00+00:00')) 180 $now $true
Assert-Result 'Bat dau dung luc hien tai' ([DateTimeOffset]::Parse('2026-09-25T02:00:00+00:00')) 60 ([DateTimeOffset]::Parse('2026-09-25T02:00:00+00:00')) $true
Assert-Result 'Bat dau 13:00' ([DateTimeOffset]::Parse('2026-09-25T06:00:00+00:00')) 60 $now $true
Assert-Result 'Khong vuot gio nghi' ([DateTimeOffset]::Parse('2026-09-25T04:00:00+00:00')) 90 $now $false
Assert-Result 'Khong bat dau 12:00' ([DateTimeOffset]::Parse('2026-09-25T05:00:00+00:00')) 60 $now $false
Assert-Result 'Khong vuot 23:00' ([DateTimeOffset]::Parse('2026-09-25T15:30:00+00:00')) 60 $now $false
Assert-Result 'Dung slot 30 phut' ([DateTimeOffset]::Parse('2026-09-25T02:15:00+00:00')) 60 $now $false
Assert-Result 'Dung giay le' ([DateTimeOffset]::Parse('2026-09-25T02:00:00.001+00:00')) 60 $now $false
Assert-Result 'Chi thoi luong cho phep' ([DateTimeOffset]::Parse('2026-09-25T02:00:00+00:00')) 30 $now $false
Assert-Result 'Khong qua khu' ([DateTimeOffset]::Parse('2026-09-25T00:30:00+00:00')) 60 $now $false
Assert-Result 'Ngay thu 30 hop le' ([DateTimeOffset]::Parse('2026-10-25T02:00:00+00:00')) 60 $now $true
Assert-Result 'Ngay thu 31 khong hop le' ([DateTimeOffset]::Parse('2026-10-26T02:00:00+00:00')) 60 $now $false
Assert-Result 'StartTime chuan UTC' ([DateTimeOffset]::Parse('2026-09-25T09:00:00+07:00')) 60 $now $false
