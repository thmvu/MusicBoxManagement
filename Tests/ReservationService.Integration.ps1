param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path

$databaseName = 'MusicBoxReservationTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=$databaseName;Integrated Security=True"

function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
function Create-Booking($roomId, $name, $phone, $startUtc, $minutes) {
    $context = New-Context
    try {
        $service = [MusicBoxManagement.Services.ReservationService]::new($context, [MusicBoxManagement.Services.SystemClock]::new())
        return $service.CreateGuest($roomId, $name, $phone, $startUtc, $minutes)
    }
    finally { $context.Dispose() }
}
function Assert-True($condition, $message) { if (!$condition) { throw $message } }

$setup = New-Context
try {
    $setup.Database.Create()
    $roomType = [MusicBoxManagement.Models.RoomType]::new()
    $roomType.Code = 'STANDARD'; $roomType.Name = 'Standard'; $roomType.Capacity = 4
    $roomType.PricePerHour = 120000; $roomType.Amenities = 'TV'
    [void]$setup.RoomTypes.Add($roomType)
    [void]$setup.SaveChanges()
    foreach ($code in @('T1', 'T2')) {
        $room = [MusicBoxManagement.Models.Room]::new()
        $room.RoomCode = $code; $room.Name = $code; $room.RoomTypeId = $roomType.RoomTypeId
        $room.IsActive = $true; $room.CreatedAt = [DateTimeOffset]::UtcNow
        [void]$setup.Rooms.Add($room)
    }
    [void]$setup.SaveChanges()
    $rooms = @($setup.Rooms | Sort-Object RoomCode)
    $offset = [TimeSpan]::FromHours(7)
    $tomorrow = [DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(1).AddHours(9)
    $startUtc = [DateTimeOffset]::new($tomorrow, $offset).ToUniversalTime()

    $first = Create-Booking $rooms[0].RoomId 'Nguyễn Văn A' '+84 912 345 678' $startUtc 60
    Assert-True $first.Succeeded "First booking failed: $($first.Error)"
    Write-Output 'PASS create Confirmed reservation'

    $sameRoom = Create-Booking $rooms[0].RoomId 'Khách khác' '0987654321' $startUtc 60
    Assert-True (!$sameRoom.Succeeded) 'Overlapping room booking was accepted.'
    Write-Output 'PASS reject same room and roll back new customer'

    $sameCustomer = Create-Booking $rooms[1].RoomId 'Tên nhập lại' '0912345678' $startUtc 60
    Assert-True (!$sameCustomer.Succeeded) 'Overlapping customer booking was accepted.'
    Write-Output 'PASS reject same customer across rooms'

    $adjacent = Create-Booking $rooms[1].RoomId 'Tên nhập lại' '0912345678' ($startUtc.AddHours(1)) 60
    Assert-True $adjacent.Succeeded "Adjacent booking failed: $($adjacent.Error)"
    Write-Output 'PASS adjacent booking reuses customer'

    $staff = [MusicBoxManagement.Models.ApplicationUser]::new()
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'booking_test_staff'; $staff.FullName = 'Test Staff'
    [void]$setup.Users.Add($staff); [void]$setup.SaveChanges()
    $staffContext = New-Context
    try {
        $staffService = [MusicBoxManagement.Services.ReservationService]::new($staffContext, [MusicBoxManagement.Services.SystemClock]::new())
        $staffBooking = $staffService.CreateStaff($rooms[0].RoomId, 'Khách nhân viên nhập', '0987654321', ($startUtc.AddHours(2)), 60, $staff.Id)
        Assert-True $staffBooking.Succeeded "Staff booking failed: $($staffBooking.Error)"
        $staffConflict = $staffService.CreateStaff($rooms[0].RoomId, 'Khách bị trùng', '0900000000', ($startUtc.AddHours(2)), 60, $staff.Id)
        Assert-True (!$staffConflict.Succeeded) 'Staff booking bypassed room conflict.'
    }
    finally { $staffContext.Dispose() }
    Write-Output 'PASS staff creates booking with shared availability rules'

    $check = New-Context
    try {
        $customers = @($check.Customers)
        $reservations = @($check.Reservations)
        $logs = @($check.AuditLogs)
        Assert-True ($customers.Count -eq 2) "Expected 2 customers, got $($customers.Count)."
        Assert-True ($customers[0].FullName -eq 'Nguyễn Văn A') 'Guest input overwrote stored customer name.'
        Assert-True ($reservations.Count -eq 3) "Expected 3 reservations, got $($reservations.Count)."
        Assert-True (@($reservations | Where-Object Status -eq 'Confirmed').Count -eq 3) 'Reservation status is wrong.'
        Assert-True ($logs.Count -eq 3) "Expected 3 audit logs, got $($logs.Count)."
        Assert-True ($check.Reservations.Find($staffBooking.ReservationId).CreatedByUserId -eq $staff.Id) 'Staff creator missing.'
        Assert-True (@($logs | Where-Object { $_.EntityId -eq "$($staffBooking.ReservationId)" -and $_.ActorType -eq 'Staff' }).Count -eq 1) 'Staff audit missing.'
        Write-Output 'PASS persisted Customer, Reservation and AuditLog together'
    }
    finally { $check.Dispose() }
}
finally {
    $setup.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
