param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path

function Assert-Parse($name, $date, $time, $expectedValid, $expectedUtc) {
    $result = [MusicBoxManagement.Services.GuestBookingDateParser]::Parse($date, $time)
    if ($result.IsValid -ne $expectedValid) { throw "$name : wrong validity" }
    if ($expectedValid -and $result.StartTimeUtc -ne [DateTimeOffset]::Parse($expectedUtc)) { throw "$name : wrong UTC" }
    Write-Output "PASS $name"
}

Assert-Parse 'Gio Viet Nam sang UTC' '2026-09-26' '09:30' $true '2026-09-26T02:30:00+00:00'
Assert-Parse 'Ngay khong ton tai' '2026-02-30' '09:00' $false $null
Assert-Parse 'Gio sai dinh dang' '2026-09-26' '9:00' $false $null
Assert-Parse 'Khong nhan chuoi thua' '2026-09-26x' '09:00' $false $null
