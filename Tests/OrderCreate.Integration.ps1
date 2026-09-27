param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class OrderTestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path
$databaseName = 'MusicBoxOrderTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
function Line($id, $qty) {
    $line = [MusicBoxManagement.Services.OrderLineInput]::new()
    $line.ServiceId = $id; $line.Quantity = $qty
    return $line
}
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
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'order_test_staff'; $staff.FullName = 'Test Staff'
    [void]$setup.Users.Add($staff)
    $drink = [MusicBoxManagement.Models.Service]::new()
    $drink.Name = 'Coca'; $drink.Category = 'Đồ uống'; $drink.Price = 20000; $drink.IsActive = $true
    [void]$setup.Services.Add($drink)
    $food = [MusicBoxManagement.Models.Service]::new()
    $food.Name = 'Snack'; $food.Category = 'Đồ ăn'; $food.Price = 25000; $food.IsActive = $true
    [void]$setup.Services.Add($food)
    $inactive = [MusicBoxManagement.Models.Service]::new()
    $inactive.Name = 'Old'; $inactive.Category = 'Khác'; $inactive.Price = 10000; $inactive.IsActive = $false
    [void]$setup.Services.Add($inactive)
    [void]$setup.SaveChanges()
    $offset = [TimeSpan]::FromHours(7)
    $day = [DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(1)
    $now = [DateTimeOffset]::new($day.AddHours(17), $offset).ToUniversalTime()
    $session = [MusicBoxManagement.Models.RoomSession]::new()
    $session.RoomId = $room.RoomId; $session.CustomerId = $customer.CustomerId
    $session.ActualStartTime = $now; $session.Status = 'Active'; $session.HourlyRate = 120000
    $session.RoomCodeSnapshot = 'R1'; $session.RoomTypeCodeSnapshot = 'STANDARD'; $session.RoomTypeNameSnapshot = 'Standard'
    [void]$setup.RoomSessions.Add($session); [void]$setup.SaveChanges()

    $clock = [OrderTestClock]::new(); $clock.UtcNow = $now
    $context = New-Context
    try {
        $service = [MusicBoxManagement.Services.OrderService]::new($context, $clock)
        Assert-True (!$service.CreateGuest($session.RoomSessionId, '0912345678', [MusicBoxManagement.Services.OrderLineInput[]]@((Line $inactive.ServiceId 1))).Succeeded) 'Inactive service accepted.'
        Assert-True (!$service.CreateGuest($session.RoomSessionId, '0987654321', [MusicBoxManagement.Services.OrderLineInput[]]@((Line $drink.ServiceId 1))).Succeeded) 'Wrong guest phone accepted.'
        $guest = $service.CreateGuest($session.RoomSessionId, '+84 912 345 678', [MusicBoxManagement.Services.OrderLineInput[]]@((Line $drink.ServiceId 4), (Line $drink.ServiceId 6)))
        Assert-True $guest.Succeeded "Guest Order failed: $($guest.Error)"
        $served = $service.CreateByStaff($session.RoomSessionId, $staff.Id, [MusicBoxManagement.Services.OrderLineInput[]]@((Line $food.ServiceId 2)))
        Assert-True $served.Succeeded "Staff Order failed: $($served.Error)"
    }
    finally { $context.Dispose() }
    $check = New-Context
    try {
        $orders = @($check.Orders | Sort-Object OrderId)
        Assert-True ($orders.Count -eq 2) 'Expected two Orders.'
        Assert-True ($orders[0].Status -eq 'Pending' -and $orders[0].CreatedByUserId -eq $null) 'Guest status/actor wrong.'
        Assert-True ($orders[1].Status -eq 'Completed' -and $orders[1].CreatedByUserId -eq $staff.Id) 'Staff status/actor wrong.'
        $items = @($check.OrderItems | Sort-Object OrderId)
        Assert-True ($items.Count -eq 2 -and $items[0].Quantity -eq 10) 'Order items were not merged.'
        Assert-True ($items[0].ServiceNameSnapshot -eq 'Coca' -and $items[0].UnitPrice -eq 20000) 'Guest snapshot wrong.'
        Assert-True ($items[1].ServiceNameSnapshot -eq 'Snack' -and $items[1].UnitPrice -eq 25000) 'Staff snapshot wrong.'
        Assert-True (@($check.AuditLogs | Where-Object { $_.EntityName -eq 'Order' }).Count -eq 2) 'Order audit missing.'
        $check.Services.Find($drink.ServiceId).Price = 30000
        $check.Services.Find($drink.ServiceId).Name = 'New Coca'
        [void]$check.SaveChanges()
        Assert-True ($check.OrderItems.Find($items[0].OrderItemId).UnitPrice -eq 20000) 'Catalog edit changed snapshot.'
        $clock.UtcNow = $now.AddMinutes(90)
        $preview = [MusicBoxManagement.Services.BillingService]::new($check, $clock).GetPreview($session.RoomSessionId)
        Assert-True ($preview.RoomCharge -eq 180000 -and $preview.ServiceCharge -eq 50000) 'Pending order was counted in preview.'
        $lifecycle = [MusicBoxManagement.Services.OrderService]::new($check, $clock)
        Assert-True (!$lifecycle.CancelGuest($orders[0].OrderId, '0987654321').Succeeded) 'Wrong phone cancelled order.'
        Assert-True $lifecycle.ConfirmByStaff($orders[0].OrderId, $staff.Id).Succeeded 'Staff confirmation failed.'
        Assert-True (!$lifecycle.ConfirmByStaff($orders[0].OrderId, $staff.Id).Succeeded) 'Repeated confirmation succeeded.'
        Assert-True (!$lifecycle.CancelGuest($orders[0].OrderId, '0912345678').Succeeded) 'Completed order was cancelled.'
        $check.Entry($orders[0]).Reload()
        $afterConfirm = [MusicBoxManagement.Services.BillingService]::new($check, $clock).GetPreview($session.RoomSessionId)
        Assert-True ($afterConfirm.ServiceCharge -eq 250000 -and $afterConfirm.TotalAmount -eq 430000) 'Confirmed order not reflected in preview.'
        $clock.UtcNow = $now
        $pending = $lifecycle.CreateGuest($session.RoomSessionId, '0912345678', [MusicBoxManagement.Services.OrderLineInput[]]@((Line $drink.ServiceId 1)))
        Assert-True $pending.Succeeded 'Second Pending order failed.'
        Assert-True $lifecycle.CancelGuest($pending.OrderId, '0912345678').Succeeded 'Guest cancellation failed.'
        Assert-True (!$lifecycle.ConfirmByStaff($pending.OrderId, $staff.Id).Succeeded) 'Cancelled order was confirmed.'
        $clock.UtcNow = $now.AddMinutes(90)
        Assert-True (([MusicBoxManagement.Services.BillingService]::new($check, $clock).GetPreview($session.RoomSessionId)).ServiceCharge -eq 250000) 'Cancelled order was counted.'
    }
    finally { $check.Dispose() }
    Write-Output 'PASS Order creation, transition, guest ownership, billing preview, snapshot and audit'
}
finally {
    $setup.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
