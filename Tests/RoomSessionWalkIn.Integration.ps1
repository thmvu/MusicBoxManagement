param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class WalkInTestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path

$databaseName = 'MusicBoxWalkInTest_' + [Guid]::NewGuid().ToString('N')
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
    $bookedCustomer = [MusicBoxManagement.Models.Customer]::new()
    $bookedCustomer.FullName = 'Booked'; $bookedCustomer.PhoneNumber = '0987654321'
    [void]$setup.Customers.Add($bookedCustomer)
    $staff = [MusicBoxManagement.Models.ApplicationUser]::new()
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'walkin_test_staff'; $staff.FullName = 'Test Staff'
    [void]$setup.Users.Add($staff); [void]$setup.SaveChanges()

    $offset = [TimeSpan]::FromHours(7)
    $day = [DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(1)
    $now = [DateTimeOffset]::new($day.AddHours(17).AddMinutes(5), $offset).ToUniversalTime()
    $booking = [MusicBoxManagement.Models.Reservation]::new()
    $booking.RoomId = $room.RoomId; $booking.CustomerId = $bookedCustomer.CustomerId
    $booking.StartTime = $now.AddMinutes(-5); $booking.EndTime = $now.AddMinutes(55)
    $booking.Status = 'Confirmed'; $booking.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$setup.Reservations.Add($booking); [void]$setup.SaveChanges()

    $clock = [WalkInTestClock]::new(); $clock.UtcNow = $now
    $context = New-Context
    try {
        $service = [MusicBoxManagement.Services.RoomSessionService]::new($context, $clock)
        $blocked = $service.WalkIn($room.RoomId, 'Walk In', '0912345678', $staff.Id)
        Assert-True (!$blocked.Succeeded) 'Current booking did not block walk-in.'
    }
    finally { $context.Dispose() }
    $check = New-Context
    try {
        Assert-True (@($check.Customers).Count -eq 1) 'Rejected walk-in left a new customer.'
        Assert-True (@($check.RoomSessions).Count -eq 0) 'Rejected walk-in left a session.'
    }
    finally { $check.Dispose() }

    $booking.Status = 'Cancelled'; [void]$setup.SaveChanges()
    $next = [MusicBoxManagement.Models.Reservation]::new()
    $next.RoomId = $room.RoomId; $next.CustomerId = $bookedCustomer.CustomerId
    $next.StartTime = [DateTimeOffset]::new($day.AddHours(20), $offset).ToUniversalTime()
    $next.EndTime = $next.StartTime.AddHours(1)
    $next.Status = 'Confirmed'; $next.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$setup.Reservations.Add($next); [void]$setup.SaveChanges()

    $context = New-Context
    try {
        $service = [MusicBoxManagement.Services.RoomSessionService]::new($context, $clock)
        $result = $service.WalkIn($room.RoomId, 'Walk In', '+84 912 345 678', $staff.Id)
        Assert-True $result.Succeeded "Walk-in failed: $($result.Error)"
        Assert-True ($result.WarningEndUtc -eq $next.StartTime) 'Next booking warning incorrect.'
        Assert-True (!$service.WalkIn($room.RoomId, 'Another', '0900000000', $staff.Id).Succeeded) 'Second active room session accepted.'
    }
    finally { $context.Dispose() }

    $check = New-Context
    try {
        $sessions = @($check.RoomSessions)
        Assert-True ($sessions.Count -eq 1) 'Expected one walk-in session.'
        $session = $sessions[0]
        Assert-True ($session.ReservationId -eq $null -and $session.ExpectedEndTime -eq $null) 'Walk-in created booking duration.'
        Assert-True ($session.ActualStartTime -eq $now -and $session.Status -eq 'Active') 'Walk-in time/status wrong.'
        Assert-True ($session.HourlyRate -eq 120000 -and $session.RoomCodeSnapshot -eq 'R1') 'Walk-in snapshot wrong.'
        Assert-True (@($check.AuditLogs | Where-Object { $_.Action -eq 'WalkIn' }).Count -eq 1) 'Walk-in audit missing.'
        $earlier = [MusicBoxManagement.Models.Reservation]::new()
        $earlier.RoomId = $room.RoomId; $earlier.CustomerId = $bookedCustomer.CustomerId
        $earlier.StartTime = [DateTimeOffset]::new($day.AddHours(18), $offset).ToUniversalTime()
        $earlier.EndTime = $earlier.StartTime.AddHours(1)
        $earlier.Status = 'Confirmed'; $earlier.CreatedAt = [DateTimeOffset]::UtcNow
        [void]$check.Reservations.Add($earlier); [void]$check.SaveChanges()
        $warning = ([MusicBoxManagement.Services.RoomSessionService]::new($check, $clock)).GetWalkInWarning($session.RoomSessionId)
        Assert-True ($warning -eq $earlier.StartTime) 'Warning did not update after a new booking.'
    }
    finally { $check.Dispose() }
    Write-Output 'PASS walk-in rollback, future warning, active uniqueness, snapshot and audit'
}
finally {
    $setup.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
