param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class CheckInTestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path

$databaseName = 'MusicBoxCheckInTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
function Assert-True($condition, $message) { if (!$condition) { throw $message } }

$setup = New-Context
try {
    $setup.Database.Create()
    $type = [MusicBoxManagement.Models.RoomType]::new()
    $type.Code = 'STANDARD'; $type.Name = 'Standard'; $type.Capacity = 4
    $type.PricePerHour = 120000; $type.Amenities = 'TV'
    [void]$setup.RoomTypes.Add($type); [void]$setup.SaveChanges()
    $room = [MusicBoxManagement.Models.Room]::new()
    $room.RoomCode = 'R1'; $room.Name = 'Room 1'; $room.RoomTypeId = $type.RoomTypeId
    $room.IsActive = $true; $room.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$setup.Rooms.Add($room)
    $customer = [MusicBoxManagement.Models.Customer]::new()
    $customer.FullName = 'Test'; $customer.PhoneNumber = '0912345678'
    [void]$setup.Customers.Add($customer)
    $staff = [MusicBoxManagement.Models.ApplicationUser]::new()
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'checkin_test_staff'; $staff.FullName = 'Test Staff'
    [void]$setup.Users.Add($staff); [void]$setup.SaveChanges()

    $offset = [TimeSpan]::FromHours(7)
    $day = [DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(1)
    $start = [DateTimeOffset]::new($day.AddHours(20), $offset).ToUniversalTime()
    $booking = [MusicBoxManagement.Models.Reservation]::new()
    $booking.RoomId = $room.RoomId; $booking.CustomerId = $customer.CustomerId
    $booking.StartTime = $start; $booking.EndTime = $start.AddHours(2)
    $booking.Status = 'Confirmed'; $booking.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$setup.Reservations.Add($booking); [void]$setup.SaveChanges()

    $nextCustomer = [MusicBoxManagement.Models.Customer]::new()
    $nextCustomer.FullName = 'Next Guest'; $nextCustomer.PhoneNumber = '0987654321'
    [void]$setup.Customers.Add($nextCustomer); [void]$setup.SaveChanges()
    $nextBooking = [MusicBoxManagement.Models.Reservation]::new()
    $nextBooking.RoomId = $room.RoomId; $nextBooking.CustomerId = $nextCustomer.CustomerId
    $nextBooking.StartTime = $start.AddHours(2); $nextBooking.EndTime = $start.AddHours(3)
    $nextBooking.Status = 'Confirmed'; $nextBooking.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$setup.Reservations.Add($nextBooking); [void]$setup.SaveChanges()

    $clock = [CheckInTestClock]::new(); $clock.UtcNow = $start.AddMinutes(10)
    $context = New-Context
    try {
        $service = [MusicBoxManagement.Services.RoomSessionService]::new($context, $clock)
        Assert-True (!$service.CheckIn($booking.ReservationId, $staff.Id).Succeeded) 'Late check-in passed next booking.'
        $clock.UtcNow = $start.AddMinutes(15)
        Assert-True (!$service.CheckIn($booking.ReservationId, $staff.Id).Succeeded) 'Exact grace cutoff was accepted.'
        $clock.UtcNow = $start.AddHours(-1)
        $first = $service.CheckIn($booking.ReservationId, $staff.Id)
        Assert-True $first.Succeeded "Check-in failed: $($first.Error)"
        $second = $service.CheckIn($booking.ReservationId, $staff.Id)
        Assert-True ($second.Succeeded -and $second.RoomSessionId -eq $first.RoomSessionId) 'Repeat check-in made a second session.'
    }
    finally { $context.Dispose() }

    $check = New-Context
    try {
        $sessions = @($check.RoomSessions)
        Assert-True ($sessions.Count -eq 1) 'Expected exactly one session.'
        $session = $sessions[0]
        Assert-True ($session.ReservationId -eq $booking.ReservationId) 'Reservation link missing.'
        Assert-True ($session.ActualStartTime -eq $clock.UtcNow -and $session.ExpectedEndTime -eq $start.AddHours(1)) 'Session times incorrect.'
        Assert-True ($session.Status -eq 'Active' -and $session.ActualEndTime -eq $null) 'Session status incorrect.'
        Assert-True ($session.HourlyRate -eq 120000 -and $session.RoomCodeSnapshot -eq 'R1' -and $session.RoomTypeCodeSnapshot -eq 'STANDARD') 'Snapshot incorrect.'
        Assert-True ($check.Reservations.Find($booking.ReservationId).Status -eq 'CheckedIn') 'Reservation status incorrect.'
        $logs = @($check.AuditLogs | Where-Object { $_.Action -eq 'CheckIn' })
        Assert-True ($logs.Count -eq 1 -and $logs[0].ActorType -eq 'Staff' -and $logs[0].UserId -eq $staff.Id) 'Check-in audit missing.'
        $check.RoomTypes.Find($type.RoomTypeId).PricePerHour = 200000
        [void]$check.SaveChanges()
        Assert-True ($check.RoomSessions.Find($session.RoomSessionId).HourlyRate -eq 120000) 'Session rate changed with catalog.'
    }
    finally { $check.Dispose() }
    Write-Output 'PASS check-in transaction, snapshot, audit and repeated request'

    $lookupContext = New-Context
    try {
        $guestLookup = ([MusicBoxManagement.Services.ReservationService]::new($lookupContext, $clock)).LookupGuest('0912345678')
        Assert-True ($guestLookup.ActiveSessions.Count -eq 1) 'Guest lookup did not show Active session.'
        Assert-True ($guestLookup.ActiveSessions[0].CanExtend) 'Guest could not see extend action.'
        Assert-True (([MusicBoxManagement.Services.ReservationService]::new($lookupContext, $clock)).LookupGuest('0987654321').ActiveSessions.Count -eq 0) 'Other phone saw Active session.'
    }
    finally { $lookupContext.Dispose() }

    $clock.UtcNow = $start
    $context = New-Context
    try {
        $service = [MusicBoxManagement.Services.RoomSessionService]::new($context, $clock)
        Assert-True (!$service.ExtendGuest($first.RoomSessionId, 30, '0900000000').Succeeded) 'Wrong phone extended session.'
        $guestExtension = $service.ExtendGuest($first.RoomSessionId, 30, '+84 912 345 678')
        Assert-True ($guestExtension.Succeeded -and $guestExtension.NewEndUtc -eq $start.AddHours(1).AddMinutes(30)) 'Guest extension failed.'
        $staffExtension = $service.ExtendByStaff($first.RoomSessionId, 30, $staff.Id)
        Assert-True ($staffExtension.Succeeded -and $staffExtension.NewEndUtc -eq $start.AddHours(2)) 'Staff extension failed.'
        Assert-True (!$service.ExtendByStaff($first.RoomSessionId, 30, $staff.Id).Succeeded) 'Extension overlapped next booking.'
    }
    finally { $context.Dispose() }
    $check = New-Context
    try {
        Assert-True ($check.RoomSessions.Find($first.RoomSessionId).ExpectedEndTime -eq $start.AddHours(2)) 'Extended end not saved.'
        Assert-True ($check.Reservations.Find($booking.ReservationId).EndTime -eq $start.AddHours(2)) 'Reservation end was changed.'
        Assert-True ($check.RoomSessions.Find($first.RoomSessionId).HourlyRate -eq 120000) 'Extension changed snapshot rate.'
        Assert-True (@($check.AuditLogs | Where-Object { $_.Action -eq 'Extend' }).Count -eq 2) 'Extension audit count incorrect.'
    }
    finally { $check.Dispose() }
    Write-Output 'PASS Guest/Staff extension, phone check, next booking, snapshot and audit'
}
finally {
    $setup.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
