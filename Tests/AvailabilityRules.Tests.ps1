param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path

function Time($value) { [DateTimeOffset]::Parse($value) }
function Reservation($room, $customer, $start, $end, $status) {
    $item = New-Object MusicBoxManagement.Models.Reservation
    $item.RoomId = $room; $item.CustomerId = $customer
    $item.StartTime = Time $start; $item.EndTime = Time $end; $item.Status = $status
    return $item
}
function Session($room, $customer, $reservationId, $start, $end) {
    $item = New-Object MusicBoxManagement.Models.RoomSession
    $item.RoomId = $room; $item.CustomerId = $customer; $item.ReservationId = $reservationId
    $item.ActualStartTime = Time $start
    if ($end) { $item.ExpectedEndTime = Time $end }
    $item.Status = 'Active'
    return $item
}
function Assert-Conflict($name, $room, $customer, $start, $end, $now, $reservations, $sessions, $expected) {
    $actual = [MusicBoxManagement.Services.AvailabilityRules]::FindConflict(
        $room, $customer, (Time $start), (Time $end), (Time $now),
        [MusicBoxManagement.Models.Reservation[]]$reservations,
        [MusicBoxManagement.Models.RoomSession[]]$sessions)
    if ($actual -ne $expected) { throw "$name : expected '$expected', got '$actual'" }
    Write-Output "PASS $name"
}

$now = '2026-09-25T10:00:00+00:00'
$booking = Reservation 1 1 '2026-09-25T13:00:00+00:00' '2026-09-25T15:00:00+00:00' 'Confirmed'
Assert-Conflict 'Overlap cung phong' 1 2 '2026-09-25T14:00:00+00:00' '2026-09-25T16:00:00+00:00' $now @($booking) @() 'Phòng đã có lịch trong khoảng giờ này.'
Assert-Conflict 'Overlap cung khach' 2 1 '2026-09-25T14:00:00+00:00' '2026-09-25T16:00:00+00:00' $now @($booking) @() 'Khách đã có lịch trong khoảng giờ này.'
Assert-Conflict 'Sat gio khong overlap' 1 2 '2026-09-25T15:00:00+00:00' '2026-09-25T16:00:00+00:00' $now @($booking) @() $null
Assert-Conflict 'Cancelled khong giu lich' 1 2 '2026-09-25T14:00:00+00:00' '2026-09-25T16:00:00+00:00' $now @((Reservation 1 1 '2026-09-25T13:00:00+00:00' '2026-09-25T15:00:00+00:00' 'Cancelled')) @() $null
Assert-Conflict 'Het grace dung moc 15 phut' 1 2 '2026-09-25T14:00:00+00:00' '2026-09-25T16:00:00+00:00' '2026-09-25T13:15:00+00:00' @($booking) @() $null
$session = Session 1 1 7 '2026-09-25T13:10:00+00:00' '2026-09-25T15:30:00+00:00'
Assert-Conflict 'Session booking giu lich' 1 2 '2026-09-25T15:00:00+00:00' '2026-09-25T16:00:00+00:00' $now @() @($session) 'Phòng đã có lịch trong khoảng giờ này.'
Assert-Conflict 'Session booking chan cung khach' 2 1 '2026-09-25T15:00:00+00:00' '2026-09-25T16:00:00+00:00' $now @() @($session) 'Khách đã có lịch trong khoảng giờ này.'
Assert-Conflict 'Session khong chan sau ExpectedEnd' 1 2 '2026-09-25T15:30:00+00:00' '2026-09-25T16:30:00+00:00' $now @() @($session) $null
Assert-Conflict 'CheckedIn khong giu them lich' 1 2 '2026-09-25T14:00:00+00:00' '2026-09-25T15:00:00+00:00' $now @((Reservation 1 1 '2026-09-25T13:00:00+00:00' '2026-09-25T15:00:00+00:00' 'CheckedIn')) @() $null
$walkIn = Session 1 1 $null '2026-09-25T10:00:00+00:00' $null
Assert-Conflict 'Walk-in khong chan tuong lai' 1 2 '2026-09-25T13:00:00+00:00' '2026-09-25T14:00:00+00:00' $now @() @($walkIn) $null
Assert-Conflict 'Active chan start bang now' 1 2 '2026-09-25T10:00:00+00:00' '2026-09-25T11:00:00+00:00' $now @() @($walkIn) 'Phòng đang có khách sử dụng.'
Assert-Conflict 'Active customer chan start bang now' 2 1 '2026-09-25T10:00:00+00:00' '2026-09-25T11:00:00+00:00' $now @() @($walkIn) 'Khách đang sử dụng phòng khác.'
