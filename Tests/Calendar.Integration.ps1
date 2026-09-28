#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll", [string]$SqlServer = '.')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class CalendarTestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$databaseName = 'MusicBoxCalendarTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=$SqlServer;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
$db = New-Context
try {
    $db.Database.Create()
    $type = [MusicBoxManagement.Models.RoomType]::new()
    $type.Code = 'STANDARD'; $type.Name = 'Standard'; $type.Capacity = 4
    $type.PricePerHour = 120000; $type.Amenities = 'TV'
    [void]$db.RoomTypes.Add($type)
    $customer = [MusicBoxManagement.Models.Customer]::new()
    $customer.FullName = 'Calendar Guest'; $customer.PhoneNumber = '0912345678'
    [void]$db.Customers.Add($customer)
    $customer2 = [MusicBoxManagement.Models.Customer]::new()
    $customer2.FullName = 'Walk-in Guest'; $customer2.PhoneNumber = '0912345679'
    [void]$db.Customers.Add($customer2)
    [void]$db.SaveChanges()
    $room = [MusicBoxManagement.Models.Room]::new()
    $room.RoomCode = 'R1'; $room.Name = 'Room 1'; $room.RoomTypeId = $type.RoomTypeId
    $room.IsActive = $true; $room.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$db.Rooms.Add($room)
    $room2 = [MusicBoxManagement.Models.Room]::new()
    $room2.RoomCode = 'R2'; $room2.Name = 'Room 2'; $room2.RoomTypeId = $type.RoomTypeId
    $room2.IsActive = $true; $room2.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$db.Rooms.Add($room2); [void]$db.SaveChanges()
    $offset = [TimeSpan]::FromHours(7)
    $day = [DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(1)
    $start = [DateTimeOffset]::new($day.AddHours(9), $offset).ToUniversalTime()
    $reserved = [MusicBoxManagement.Models.Reservation]::new()
    $reserved.RoomId = $room.RoomId; $reserved.CustomerId = $customer.CustomerId
    $reserved.StartTime = $start.AddHours(6); $reserved.EndTime = $start.AddHours(7)
    $reserved.Status = 'Confirmed'; $reserved.CreatedAt = $start.AddDays(-1)
    [void]$db.Reservations.Add($reserved)
    $expired = [MusicBoxManagement.Models.Reservation]::new()
    $expired.RoomId = $room.RoomId; $expired.CustomerId = $customer.CustomerId
    $expired.StartTime = $start; $expired.EndTime = $start.AddHours(1)
    $expired.Status = 'Confirmed'; $expired.CreatedAt = $start.AddDays(-1)
    [void]$db.Reservations.Add($expired)
    $checkedIn = [MusicBoxManagement.Models.Reservation]::new()
    $checkedIn.RoomId = $room.RoomId; $checkedIn.CustomerId = $customer.CustomerId
    $checkedIn.StartTime = $start.AddHours(2); $checkedIn.EndTime = $start.AddHours(3)
    $checkedIn.Status = 'CheckedIn'; $checkedIn.CreatedAt = $start.AddDays(-1)
    [void]$db.Reservations.Add($checkedIn)
    $cancelled = [MusicBoxManagement.Models.Reservation]::new()
    $cancelled.RoomId = $room.RoomId; $cancelled.CustomerId = $customer.CustomerId
    $cancelled.StartTime = $start.AddHours(5); $cancelled.EndTime = $start.AddHours(6)
    $cancelled.Status = 'Cancelled'; $cancelled.CreatedAt = $start.AddDays(-1)
    [void]$db.Reservations.Add($cancelled)
    [void]$db.SaveChanges()
    $active = [MusicBoxManagement.Models.RoomSession]::new()
    $active.RoomId = $room.RoomId; $active.CustomerId = $customer.CustomerId
    $active.ReservationId = $checkedIn.ReservationId; $active.Status = 'Active'
    $active.ActualStartTime = $start.AddHours(2).AddMinutes(7)
    $active.ExpectedEndTime = $start.AddHours(3)
    $active.HourlyRate = 120000; $active.RoomCodeSnapshot = 'R1'
    $active.RoomTypeCodeSnapshot = 'STANDARD'; $active.RoomTypeNameSnapshot = 'Standard'
    [void]$db.RoomSessions.Add($active)
    $walkIn = [MusicBoxManagement.Models.RoomSession]::new()
    $walkIn.RoomId = $room2.RoomId; $walkIn.CustomerId = $customer2.CustomerId
    $walkIn.Status = 'Active'; $walkIn.ActualStartTime = $start.AddHours(4).AddMinutes(37)
    $walkIn.HourlyRate = 120000; $walkIn.RoomCodeSnapshot = 'R2'
    $walkIn.RoomTypeCodeSnapshot = 'STANDARD'; $walkIn.RoomTypeNameSnapshot = 'Standard'
    [void]$db.RoomSessions.Add($walkIn)
    $done = [MusicBoxManagement.Models.RoomSession]::new()
    $done.RoomId = $room.RoomId; $done.CustomerId = $customer.CustomerId
    $done.Status = 'Completed'; $done.ActualStartTime = $start.AddHours(1)
    $done.ActualEndTime = $start.AddHours(1).AddMinutes(47)
    $done.HourlyRate = 120000; $done.RoomCodeSnapshot = 'R1'
    $done.RoomTypeCodeSnapshot = 'STANDARD'; $done.RoomTypeNameSnapshot = 'Standard'
    [void]$db.RoomSessions.Add($done)
    [void]$db.SaveChanges()
    $clock = [CalendarTestClock]::new(); $clock.UtcNow = $start.AddHours(4).AddMinutes(55)
    $service = [MusicBoxManagement.Services.CalendarService]::new($db, $clock)
    $events = @($service.GetEvents($start, $start.AddDays(1), $null,
        [Func[int,string]] { param($id) "/reservation/$id" },
        [Func[int,string]] { param($id) "/session/$id" }))
    Assert-True ($events.Count -eq 4) 'Expected one Confirmed booking and three sessions only.'
    Assert-True (@($events | Where-Object { $_.Url -eq "/reservation/$($reserved.ReservationId)" }).Count -eq 1) 'Confirmed booking missing.'
    Assert-True (@($events | Where-Object { $_.Url -eq "/reservation/$($expired.ReservationId)" }).Count -eq 0) 'Expired Confirmed booking remains busy.'
    Assert-True (@($events | Where-Object { $_.Url -eq "/reservation/$($checkedIn.ReservationId)" }).Count -eq 0) 'Checked-in booking duplicated.'
    $walkEvent = $events | Where-Object { $_.Url -eq "/session/$($walkIn.RoomSessionId)" }
    Assert-True ($walkEvent.Start.EndsWith('13:37:00') -and $walkEvent.End.EndsWith('13:55:00')) 'Walk-in actual time wrong.'
    $doneEvent = $events | Where-Object { $_.Url -eq "/session/$($done.RoomSessionId)" }
    Assert-True ($doneEvent.End.EndsWith('10:47:00')) 'Completed end time wrong.'
    $roomEvents = @($service.GetEvents($start, $start.AddDays(1), $room.RoomId,
        [Func[int,string]] { param($id) "/reservation/$id" },
        [Func[int,string]] { param($id) "/session/$id" }))
    Assert-True ($roomEvents.Count -eq 3) 'Room filter returned a walk-in from another room.'
    Write-Output 'PASS Calendar Confirmed, checked-in, walk-in and completed intervals on SQL Server'
} finally {
    $db.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
