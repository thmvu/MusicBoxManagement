param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
function New-Booking($room, $customer, $start) {
    $item = [MusicBoxManagement.Models.Reservation]::new()
    $item.RoomId = $room; $item.CustomerId = $customer
    $item.StartTime = $start; $item.EndTime = $start.AddHours(1); $item.Status = 'Confirmed'
    return $item
}
function Check($now, $bookings, $sessions) {
    $typedBookings = [MusicBoxManagement.Models.Reservation[]]@($bookings)
    $typedSessions = [MusicBoxManagement.Models.RoomSession[]]@($sessions)
    return [MusicBoxManagement.Services.WalkInRules]::Validate(1, 1, $now, $true, $typedBookings, $typedSessions)
}

$offset = [TimeSpan]::FromHours(7)
$day = [DateTime]::SpecifyKind([DateTime]::new(2026, 9, 27), [DateTimeKind]::Unspecified)
$now = [DateTimeOffset]::new($day.AddHours(17), $offset).ToUniversalTime()
$next = New-Booking 1 2 ($now.AddHours(3))
$result = Check $now @($next) @()
Assert-True $result.IsValid 'Future booking blocked walk-in.'
Assert-True ($result.WarningEndUtc -eq $next.StartTime) 'Next room booking warning missing.'
$next.RoomId = 2; $next.CustomerId = 1
Assert-True ((Check $now @($next) @()).WarningEndUtc -eq $next.StartTime) 'Next customer booking warning missing.'
$next.RoomId = 2; $next.CustomerId = 2
$shiftEnd = [DateTimeOffset]::new($day.AddHours(23), $offset).ToUniversalTime()
Assert-True ((Check $now @($next) @()).WarningEndUtc -eq $shiftEnd) 'Shift warning incorrect.'
Write-Output 'PASS future booking accepted and earliest warning calculated'

$current = New-Booking 1 2 ($now.AddMinutes(-10))
Assert-True (!(Check $now @($current) @()).IsValid) 'Current room booking accepted.'
$current.RoomId = 2; $current.CustomerId = 1
Assert-True (!(Check $now @($current) @()).IsValid) 'Current customer booking accepted.'
$current.StartTime = $now.AddMinutes(-20)
Assert-True (Check $now @($current) @()).IsValid 'Expired Confirmed blocked walk-in.'
$active = [MusicBoxManagement.Models.RoomSession]::new()
$active.Status = 'Active'; $active.RoomId = 1; $active.CustomerId = 2
Assert-True (!(Check $now @() @($active)).IsValid) 'Active room session accepted.'
$active.RoomId = 2; $active.CustomerId = 1
Assert-True (!(Check $now @() @($active)).IsValid) 'Active customer session accepted.'
Write-Output 'PASS current booking and active session conflicts'

$lunch = [DateTimeOffset]::new($day.AddHours(12), $offset).ToUniversalTime()
$close = [DateTimeOffset]::new($day.AddHours(23), $offset).ToUniversalTime()
Assert-True (!(Check $lunch @() @()).IsValid) 'Lunch start accepted.'
Assert-True (!(Check $close @() @()).IsValid) 'Closing time accepted.'
Write-Output 'PASS shift boundaries'
