param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
function Check($session, $now, $minutes, $bookings) {
    $typed = [MusicBoxManagement.Models.Reservation[]]@($bookings)
    return [MusicBoxManagement.Services.SessionExtendRules]::Validate($session, $now, $minutes, $typed)
}
$offset = [TimeSpan]::FromHours(7)
$day = [DateTime]::SpecifyKind([DateTime]::new(2026, 9, 27), [DateTimeKind]::Unspecified)
$start = [DateTimeOffset]::new($day.AddHours(20), $offset).ToUniversalTime()
$session = [MusicBoxManagement.Models.RoomSession]::new()
$session.RoomId = 1; $session.CustomerId = 1; $session.ReservationId = 1
$session.ActualStartTime = $start; $session.ExpectedEndTime = $start.AddHours(2)
$session.Status = 'Active'
Assert-True ((Check $session $session.ExpectedEndTime 30 @()).IsValid) 'Exact expected end rejected.'
Assert-True (!(Check $session ($session.ExpectedEndTime.AddTicks(1)) 30 @()).IsValid) 'Overdue extension accepted.'
Assert-True (!(Check $session $start 15 @()).IsValid) 'Invalid duration accepted.'
Assert-True ((Check $session $start 60 @()).NewEndUtc -eq $start.AddHours(3)) 'New end incorrect.'
Assert-True (!(Check $session $start 90 @()).IsValid) '90 minutes accepted.'
Write-Output 'PASS extension duration and exact expected-end cutoff'
$next = [MusicBoxManagement.Models.Reservation]::new()
$next.RoomId = 1; $next.CustomerId = 2; $next.Status = 'Confirmed'
$next.StartTime = $start.AddHours(2).AddMinutes(30); $next.EndTime = $next.StartTime.AddHours(1)
Assert-True (!(Check $session $start 60 @($next)).IsValid) 'Room booking overlap accepted.'
$next.RoomId = 2; $next.CustomerId = 1
Assert-True (!(Check $session $start 60 @($next)).IsValid) 'Customer booking overlap accepted.'
Assert-True ((Check $session $start 30 @($next)).IsValid) 'Adjacent booking rejected.'
$next.Status = 'Cancelled'
Assert-True ((Check $session $start 60 @($next)).IsValid) 'Cancelled booking blocked extension.'
Write-Output 'PASS room/customer overlap and adjacency'
$session.ExpectedEndTime = $start.AddHours(2).AddMinutes(30)
Assert-True ((Check $session $start 30 @()).IsValid) 'End exactly 23:00 rejected.'
Assert-True (!(Check $session $start 60 @()).IsValid) 'Past closing accepted.'
$session.ReservationId = $null
Assert-True (!(Check $session $start 30 @()).IsValid) 'Walk-in extension accepted.'
Write-Output 'PASS closing shift and walk-in rejection'
