param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class NoShowTestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path

$databaseName = 'MusicBoxNoShowTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
function New-Booking($context, $customerId, $roomId, $start, $status) {
    $booking = [MusicBoxManagement.Models.Reservation]::new()
    $booking.CustomerId = $customerId; $booking.RoomId = $roomId
    $booking.StartTime = $start; $booking.EndTime = $start.AddHours(1)
    $booking.Status = $status; $booking.CreatedAt = $start.AddHours(-4)
    [void]$context.Reservations.Add($booking); [void]$context.SaveChanges()
    return $booking.ReservationId
}

$setup = New-Context
try {
    $setup.Database.Create()
    $type = [MusicBoxManagement.Models.RoomType]::new()
    $type.Code = 'STANDARD'; $type.Name = 'Standard'; $type.Capacity = 4
    $type.PricePerHour = 100000; $type.Amenities = 'TV'
    [void]$setup.RoomTypes.Add($type); [void]$setup.SaveChanges()
    $room = [MusicBoxManagement.Models.Room]::new()
    $room.RoomCode = 'R1'; $room.Name = 'Room 1'; $room.RoomTypeId = $type.RoomTypeId
    $room.IsActive = $true; $room.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$setup.Rooms.Add($room)
    $customer = [MusicBoxManagement.Models.Customer]::new()
    $customer.FullName = 'Test'; $customer.PhoneNumber = '0912345678'
    [void]$setup.Customers.Add($customer); [void]$setup.SaveChanges()

    $now = [DateTimeOffset]::UtcNow
    $expired = New-Booking $setup $customer.CustomerId $room.RoomId ($now.AddMinutes(-15)) 'Confirmed'
    $live = New-Booking $setup $customer.CustomerId $room.RoomId ($now.AddMinutes(-15).AddTicks(1)) 'Confirmed'
    $cancelled = New-Booking $setup $customer.CustomerId $room.RoomId ($now.AddMinutes(-30)) 'Cancelled'
    $clock = [NoShowTestClock]::new(); $clock.UtcNow = $now
    $context = New-Context
    try {
        $service = [MusicBoxManagement.Services.NoShowService]::new($context, $clock)
        Assert-True ($service.ProcessExpired() -eq 1) 'Expected one NoShow at exact 15-minute cutoff.'
        Assert-True ($service.ProcessExpired() -eq 0) 'Repeated pass changed booking twice.'
    }
    finally { $context.Dispose() }
    $check = New-Context
    try {
        Assert-True ($check.Reservations.Find($expired).Status -eq 'NoShow') 'Expired booking not changed.'
        Assert-True ($check.Reservations.Find($live).Status -eq 'Confirmed') 'Live booking changed early.'
        Assert-True ($check.Reservations.Find($cancelled).Status -eq 'Cancelled') 'Cancelled booking changed.'
        $logs = @($check.AuditLogs | Where-Object { $_.Action -eq 'NoShow' })
        Assert-True ($logs.Count -eq 1) 'Expected one NoShow audit.'
        Assert-True ($logs[0].ActorType -eq 'System' -and $logs[0].EntityId -eq "$expired") 'Audit fields incorrect.'
    }
    finally { $check.Dispose() }
    Write-Output 'PASS NoShow exact cutoff, untouched states, idempotence and audit'
}
finally {
    $setup.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
