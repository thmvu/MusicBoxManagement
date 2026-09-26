param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path

function At($value) { [DateTimeOffset]::Parse($value) }
function Booking($status, $start, $end) {
    $item = [MusicBoxManagement.Models.Reservation]::new()
    $item.RoomId = 1; $item.Status = $status
    $item.StartTime = At $start; $item.EndTime = At $end
    return $item
}
function Session($status, $reservationId, $start, $expected, $actualEnd) {
    $item = [MusicBoxManagement.Models.RoomSession]::new()
    $item.RoomId = 1; $item.Status = $status; $item.ReservationId = $reservationId
    $item.ActualStartTime = At $start
    if ($expected) { $item.ExpectedEndTime = At $expected }
    if ($actualEnd) { $item.ActualEndTime = At $actualEnd }
    return $item
}
function Slot($slots, $label) { $slots | Where-Object { $_.LocalStartLabel -eq $label } | Select-Object -First 1 }
function Assert-State($slots, $label, $expected) {
    $actual = (Slot $slots $label).Status
    if ($actual -ne $expected) { throw "$label : expected $expected, got $actual" }
    Write-Output "PASS $label = $expected"
}

$day = [DateTime]::Parse('2026-09-26')
$beforeDay = At '2026-09-25T00:00:00+00:00'
$booking = Booking 'Confirmed' '2026-09-26T03:00:00+00:00' '2026-09-26T04:00:00+00:00'
$slots = [MusicBoxManagement.Services.GuestRoomScheduleRules]::Build(1, $day, $beforeDay,
    [MusicBoxManagement.Models.Reservation[]]@($booking), [MusicBoxManagement.Models.RoomSession[]]@())
if ($slots.Count -ne 26) { throw "Expected 26 slots, got $($slots.Count)" }
Assert-State $slots '10:00' 'Busy'
Assert-State $slots '10:30' 'Busy'
Assert-State $slots '11:00' 'Available'

$expired = [MusicBoxManagement.Services.GuestRoomScheduleRules]::Build(1, $day, (At '2026-09-26T03:15:00+00:00'),
    [MusicBoxManagement.Models.Reservation[]]@($booking), [MusicBoxManagement.Models.RoomSession[]]@())
Assert-State $expired '10:30' 'Available'

$walkIn = Session 'Active' $null '2026-09-26T06:00:00+00:00' $null $null
$walkInSlots = [MusicBoxManagement.Services.GuestRoomScheduleRules]::Build(1, $day, (At '2026-09-26T06:10:00+00:00'),
    [MusicBoxManagement.Models.Reservation[]]@(), [MusicBoxManagement.Models.RoomSession[]]@($walkIn))
Assert-State $walkInSlots '13:00' 'Busy'
Assert-State $walkInSlots '13:30' 'Available'
$nextDay = [MusicBoxManagement.Services.GuestRoomScheduleRules]::Build(1, $day.AddDays(1), (At '2026-09-26T06:10:00+00:00'),
    [MusicBoxManagement.Models.Reservation[]]@(), [MusicBoxManagement.Models.RoomSession[]]@($walkIn))
Assert-State $nextDay '09:00' 'Available'

$reservedSession = Session 'Active' 5 '2026-09-26T06:05:00+00:00' '2026-09-26T07:00:00+00:00' $null
$reservedSlots = [MusicBoxManagement.Services.GuestRoomScheduleRules]::Build(1, $day, (At '2026-09-26T06:10:00+00:00'),
    [MusicBoxManagement.Models.Reservation[]]@(), [MusicBoxManagement.Models.RoomSession[]]@($reservedSession))
Assert-State $reservedSlots '13:30' 'Busy'
Assert-State $reservedSlots '14:00' 'Available'

$completed = Session 'Completed' $null '2026-09-26T02:05:00+00:00' $null '2026-09-26T02:40:00+00:00'
$history = [MusicBoxManagement.Services.GuestRoomScheduleRules]::Build(1, $day, (At '2026-09-26T04:00:00+00:00'),
    [MusicBoxManagement.Models.Reservation[]]@(), [MusicBoxManagement.Models.RoomSession[]]@($completed))
Assert-State $history '09:00' 'Busy'
Assert-State $history '09:30' 'Busy'
Assert-State $history '10:00' 'Past'
