#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll", [string]$SqlServer = '.')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class CheckoutTestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$databaseName = 'MusicBoxCheckoutTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=$SqlServer;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
$setup = New-Context
try {
    $setup.Database.Create()
    $type = [MusicBoxManagement.Models.RoomType]::new()
    $type.Code = 'STANDARD'; $type.Name = 'Standard'; $type.Capacity = 4
    $type.PricePerHour = 120000; $type.Amenities = 'TV'
    [void]$setup.RoomTypes.Add($type)
    $customer = [MusicBoxManagement.Models.Customer]::new()
    $customer.FullName = 'Test Guest'; $customer.PhoneNumber = '0912345678'
    [void]$setup.Customers.Add($customer)
    $staff = [MusicBoxManagement.Models.ApplicationUser]::new()
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'checkout_test_staff'; $staff.FullName = 'Test Staff'
    [void]$setup.Users.Add($staff)
    $service = [MusicBoxManagement.Models.Service]::new()
    $service.Name = 'Water'; $service.Category = 'Đồ uống'; $service.Price = 20000; $service.IsActive = $true
    [void]$setup.Services.Add($service)
    [void]$setup.SaveChanges()
    $room = [MusicBoxManagement.Models.Room]::new()
    $room.RoomCode = 'R1'; $room.Name = 'Room 1'; $room.RoomTypeId = $type.RoomTypeId
    $room.IsActive = $true; $room.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$setup.Rooms.Add($room); [void]$setup.SaveChanges()
    $offset = [TimeSpan]::FromHours(7)
    $day = [DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(1)
    $start = [DateTimeOffset]::new($day.AddHours(17), $offset).ToUniversalTime()
    $reservation = [MusicBoxManagement.Models.Reservation]::new()
    $reservation.RoomId = $room.RoomId; $reservation.CustomerId = $customer.CustomerId
    $reservation.StartTime = $start; $reservation.EndTime = $start.AddHours(2)
    $reservation.Status = 'CheckedIn'; $reservation.CreatedAt = $start.AddDays(-1)
    [void]$setup.Reservations.Add($reservation); [void]$setup.SaveChanges()
    $session = [MusicBoxManagement.Models.RoomSession]::new()
    $session.RoomId = $room.RoomId; $session.CustomerId = $customer.CustomerId
    $session.ReservationId = $reservation.ReservationId; $session.Status = 'Active'
    $session.ActualStartTime = $start; $session.ExpectedEndTime = $start.AddHours(2)
    $session.HourlyRate = 120000; $session.RoomCodeSnapshot = 'R1'
    $session.RoomTypeCodeSnapshot = 'STANDARD'; $session.RoomTypeNameSnapshot = 'Standard'
    [void]$setup.RoomSessions.Add($session); [void]$setup.SaveChanges()
    $completed = [MusicBoxManagement.Models.Order]::new()
    $completed.RoomSessionId = $session.RoomSessionId; $completed.Status = 'Completed'; $completed.CreatedAt = $start.AddMinutes(10)
    $completedLine = [MusicBoxManagement.Models.OrderItem]::new()
    $completedLine.ServiceId = $service.ServiceId; $completedLine.ServiceNameSnapshot = 'Water'
    $completedLine.Quantity = 2; $completedLine.UnitPrice = 20000
    [void]$completed.Items.Add($completedLine); [void]$setup.Orders.Add($completed)
    $pending = [MusicBoxManagement.Models.Order]::new()
    $pending.RoomSessionId = $session.RoomSessionId; $pending.Status = 'Pending'; $pending.CreatedAt = $start.AddMinutes(20)
    $pendingLine = [MusicBoxManagement.Models.OrderItem]::new()
    $pendingLine.ServiceId = $service.ServiceId; $pendingLine.ServiceNameSnapshot = 'Water'
    $pendingLine.Quantity = 1; $pendingLine.UnitPrice = 20000
    [void]$pending.Items.Add($pendingLine); [void]$setup.Orders.Add($pending)
    [void]$setup.SaveChanges()

    $clock = [CheckoutTestClock]::new(); $clock.UtcNow = $start.AddMinutes(60)
    $context = New-Context
    try {
        $checkout = [MusicBoxManagement.Services.CheckoutService]::new($context, $clock)
        $preview = $checkout.GetPreview($session.RoomSessionId)
        Assert-True ($preview.RoomCharge -eq 120000 -and $preview.ServiceCharge -eq 40000) 'Preview amounts wrong.'
        Assert-True ($context.RoomSessions.Find($session.RoomSessionId).Status -eq 'Active') 'Preview modified session.'
        Assert-True (@($context.Invoices).Count -eq 0) 'Preview created invoice.'
        Assert-True (!$checkout.Confirm($session.RoomSessionId, $staff.Id, 'Card').Succeeded) 'Invalid payment method accepted.'
        $clock.UtcNow = $start.AddMinutes(80)
        $result = $checkout.Confirm($session.RoomSessionId, $staff.Id, 'Cash')
        Assert-True $result.Succeeded "Checkout failed: $($result.Error)"
    } finally { $context.Dispose() }
    $check = New-Context
    try {
        $invoice = $check.Invoices.Find($result.InvoiceId)
        Assert-True ($invoice -ne $null -and $invoice.RoomCharge -eq 160000 -and $invoice.ServiceCharge -eq 40000 -and $invoice.TotalAmount -eq 200000) 'Final money was not recalculated.'
        Assert-True ($invoice.PaymentMethod -eq 'Cash' -and $invoice.ProcessedByNameSnapshot -eq 'Test Staff' -and $invoice.PaidAt -eq $clock.UtcNow) 'Invoice snapshots wrong.'
        Assert-True ($check.RoomSessions.Find($session.RoomSessionId).Status -eq 'Completed') 'Session not completed.'
        Assert-True ($check.RoomSessions.Find($session.RoomSessionId).ActualEndTime -eq $invoice.PaidAt) 'Session end differs from PaidAt.'
        Assert-True ($check.Reservations.Find($reservation.ReservationId).Status -eq 'Completed') 'Reservation not completed.'
        Assert-True ($check.Orders.Find($pending.OrderId).Status -eq 'Cancelled') 'Pending order not cancelled.'
        $clock.UtcNow = $start.AddMinutes(100)
        $again = [MusicBoxManagement.Services.CheckoutService]::new($check, $clock).Confirm($session.RoomSessionId, $staff.Id, 'Card')
        Assert-True ($again.Succeeded -and $again.InvoiceId -eq $invoice.InvoiceId) 'Repeated Confirm created different invoice.'
        Assert-True (@($check.Invoices).Count -eq 1 -and $check.Invoices.Find($invoice.InvoiceId).PaymentMethod -eq 'Cash') 'Repeated Confirm changed invoice.'
        Assert-True (([MusicBoxManagement.Services.CheckoutService]::new($check, $clock).GetPreview($session.RoomSessionId)) -eq $null) 'Completed session still has preview.'
    } finally { $check.Dispose() }
    Write-Output 'PASS Checkout preview, final amount, pending cancellation, reservation, idempotency and snapshots on SQL Server'
} finally {
    $setup.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
