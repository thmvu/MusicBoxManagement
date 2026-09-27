param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
function New-Booking($start, $minutes) {
    $booking = [MusicBoxManagement.Models.Reservation]::new()
    $booking.ReservationId = 1; $booking.RoomId = 1; $booking.CustomerId = 1
    $booking.StartTime = $start; $booking.EndTime = $start.AddMinutes($minutes)
    $booking.Status = 'Confirmed'
    return $booking
}
function Check($booking, $now, $otherBookings, $sessions) {
    $bookings = [MusicBoxManagement.Models.Reservation[]]@($otherBookings)
    $activeSessions = [MusicBoxManagement.Models.RoomSession[]]@($sessions)
    return [MusicBoxManagement.Services.CheckInRules]::Validate($booking, $now, $true, $bookings, $activeSessions)
}

$offset = [TimeSpan]::FromHours(7)
$day = [DateTime]::SpecifyKind([DateTime]::new(2026, 9, 27), [DateTimeKind]::Unspecified)
$start = [DateTimeOffset]::new($day.AddHours(20), $offset).ToUniversalTime()
$booking = New-Booking $start 120
$early = Check $booking ($start.AddHours(-1)) @() @()
Assert-True $early.IsValid 'Early check-in over 30 minutes was rejected.'
Assert-True ($early.CandidateEndUtc -eq $start.AddHours(1)) 'Early candidate end incorrect.'
$late = Check $booking ($start.AddMinutes(10)) @() @()
Assert-True $late.IsValid 'Check-in at +10 was rejected.'
Assert-True ($late.CandidateEndUtc -eq $start.AddHours(2).AddMinutes(10)) 'Late candidate end incorrect.'
Assert-True (!(Check $booking ($start.AddMinutes(15)) @() @()).IsValid) 'Exact +15 was accepted.'
Write-Output 'PASS early check-in, duration and exact grace cutoff'

$next = New-Booking ($start.AddHours(2)) 60
$next.ReservationId = 2
Assert-True (!(Check $booking ($start.AddMinutes(10)) @($next) @()).IsValid) 'Overlapping next booking accepted.'
$next.RoomId = 2
Assert-True (!(Check $booking ($start.AddMinutes(10)) @($next) @()).IsValid) 'Customer overlap accepted.'
$next.CustomerId = 2
Assert-True (Check $booking ($start.AddMinutes(10)) @($next) @()).IsValid 'Unrelated booking blocked.'
Write-Output 'PASS room and customer future booking conflicts'

$morning = [DateTimeOffset]::new($day.AddHours(11), $offset).ToUniversalTime()
Assert-True (!(Check (New-Booking $morning 60) ($morning.AddMinutes(10)) @() @()).IsValid) 'Lunch crossing accepted.'
$active = [MusicBoxManagement.Models.RoomSession]::new()
$active.Status = 'Active'; $active.RoomId = 1; $active.CustomerId = 2
Assert-True (!(Check $booking ($start.AddHours(-1)) @() @($active)).IsValid) 'Active room accepted.'
$active.RoomId = 2; $active.CustomerId = 1
Assert-True (!(Check $booking ($start.AddHours(-1)) @() @($active)).IsValid) 'Active customer accepted.'
Write-Output 'PASS opening shift and active session conflicts'
