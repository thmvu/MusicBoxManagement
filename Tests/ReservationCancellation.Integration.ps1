param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path

$databaseName = 'MusicBoxCancelTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
function Cancel-Guest($id, $phone, $now) {
    $context = New-Context
    try {
        $clock = [TestClock]::new(); $clock.UtcNow = $now
        return ([MusicBoxManagement.Services.ReservationService]::new($context, $clock)).CancelGuest($id, $phone)
    }
    finally { $context.Dispose() }
}
function New-Reservation($context, $customerId, $roomId, $start) {
    $booking = [MusicBoxManagement.Models.Reservation]::new()
    $booking.CustomerId = $customerId; $booking.RoomId = $roomId
    $booking.StartTime = $start; $booking.EndTime = $start.AddHours(1)
    $booking.Status = 'Confirmed'; $booking.CreatedAt = [DateTimeOffset]::UtcNow
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

    $start = [DateTimeOffset]::UtcNow.AddDays(1)
    $id = New-Reservation $setup $customer.CustomerId $room.RoomId $start
    $wrongPhone = Cancel-Guest $id '0987654321' ($start.AddHours(-3))
    Assert-True (!$wrongPhone.Succeeded) 'Wrong phone cancelled booking.'
    $tooLate = Cancel-Guest $id '0912345678' ($start.AddHours(-2).AddTicks(1))
    Assert-True (!$tooLate.Succeeded) 'Guest cancellation after cutoff succeeded.'
    $onCutoff = Cancel-Guest $id '+84 912 345 678' ($start.AddHours(-2))
    Assert-True $onCutoff.Succeeded "Guest cancellation on cutoff failed: $($onCutoff.Error)"
    Assert-True (!(Cancel-Guest $id '0912345678' ($start.AddHours(-3))).Succeeded) 'Cancelled booking changed twice.'

    $check = New-Context
    try {
        $saved = $check.Reservations.Find($id)
        Assert-True ($saved.Status -eq 'Cancelled') 'Status not persisted.'
        Assert-True ($saved.CancellationReason -eq 'Customer cancelled online') 'Guest reason incorrect.'
        Assert-True (@($check.AuditLogs | Where-Object { $_.Action -eq 'Cancel' }).Count -eq 1) 'Expected one cancel audit.'
    }
    finally { $check.Dispose() }
    Write-Output 'PASS Guest phone, 2-hour cutoff, terminal status and audit'

    $staffId = New-Reservation $setup $customer.CustomerId $room.RoomId ($start.AddHours(2))
    $context = New-Context
    try {
        $clock = [TestClock]::new(); $clock.UtcNow = $start.AddHours(2).AddMinutes(14)
        $service = [MusicBoxManagement.Services.ReservationService]::new($context, $clock)
        Assert-True (!$service.CancelByStaff($staffId, ' ', 'user-id').Succeeded) 'Blank staff reason accepted.'
    }
    finally { $context.Dispose() }
    Assert-True (!(Cancel-Guest $staffId '0912345678' ($start.AddHours(2).AddMinutes(15))).Succeeded) 'Expired Confirmed cancelled.'
    $user = [MusicBoxManagement.Models.ApplicationUser]::new()
    $user.Id = [Guid]::NewGuid().ToString('N'); $user.UserName = 'cancel_test_staff'; $user.FullName = 'Test Staff'
    [void]$setup.Users.Add($user); [void]$setup.SaveChanges()
    $staffId2 = New-Reservation $setup $customer.CustomerId $room.RoomId ($start.AddHours(3))
    $context = New-Context
    try {
        $clock = [TestClock]::new(); $clock.UtcNow = $start.AddHours(3).AddMinutes(14)
        $result = ([MusicBoxManagement.Services.ReservationService]::new($context, $clock)).CancelByStaff($staffId2, 'Khach bao ban', $user.Id)
        Assert-True $result.Succeeded "Staff cancellation failed: $($result.Error)"
    }
    finally { $context.Dispose() }
    $check = New-Context
    try {
        Assert-True ($check.Reservations.Find($staffId2).CancellationReason -eq 'Khach bao ban') 'Staff reason missing.'
        Assert-True (@($check.AuditLogs | Where-Object { $_.Action -eq 'Cancel' -and $_.UserId -eq $user.Id }).Count -eq 1) 'Staff audit missing.'
    }
    finally { $check.Dispose() }
    Write-Output 'PASS staff reason, cancellation within grace period, expired Confirmed and audit'
}
finally {
    $setup.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
