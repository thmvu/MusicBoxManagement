param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) { throw "$message : expected $expected, got $actual" }
}
$start = [DateTimeOffset]::new(2026, 9, 27, 10, 0, 0, [TimeSpan]::FromHours(7))
$preview = [MusicBoxManagement.Services.BillingService]::Calculate($start, 120000, 25000, $start.AddMinutes(90))
Assert-Equal $preview.RoomCharge 180000 'Room charge'
Assert-Equal $preview.ServiceCharge 25000 'Completed service charge'
Assert-Equal $preview.TotalAmount 205000 'Total'
$half = [MusicBoxManagement.Services.BillingService]::Calculate($start, 1, 0, $start.AddMinutes(30))
Assert-Equal $half.RoomCharge 1 'Midpoint rounds away from zero'
$exact = [MusicBoxManagement.Services.BillingService]::Calculate($start, 120000, 0, $start.AddMinutes(1.5))
Assert-Equal $exact.RoomCharge 3000 'Seconds remain in calculation'
Write-Output 'PASS Billing preview elapsed time, total and rounding'
