#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll", [string]$SqlServer = '.')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
Add-Type -TypeDefinition @'
using System;
using MusicBoxManagement.Services;
public sealed class ReportTestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
'@ -ReferencedAssemblies (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$databaseName = 'MusicBoxReportTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=$SqlServer;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
$db = New-Context
try {
    $db.Database.Create()
    $offset = [TimeSpan]::FromHours(7)
    $day = [DateTimeOffset]::new(([DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(-1)), $offset).ToUniversalTime()
    $type = [MusicBoxManagement.Models.RoomType]::new()
    $type.Code = 'STANDARD'; $type.Name = 'Standard'; $type.Capacity = 4
    $type.PricePerHour = 120000; $type.Amenities = 'TV'
    [void]$db.RoomTypes.Add($type)
    $customer = [MusicBoxManagement.Models.Customer]::new()
    $customer.FullName = 'Report Guest'; $customer.PhoneNumber = '0912345678'
    [void]$db.Customers.Add($customer)
    $staff = [MusicBoxManagement.Models.ApplicationUser]::new()
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'report_test_staff'; $staff.FullName = 'Staff'
    [void]$db.Users.Add($staff)
    $serviceItem = [MusicBoxManagement.Models.Service]::new()
    $serviceItem.Name = 'Water'; $serviceItem.Category = 'Đồ uống'; $serviceItem.Price = 20000; $serviceItem.IsActive = $false
    [void]$db.Services.Add($serviceItem)
    [void]$db.SaveChanges()
    $room = [MusicBoxManagement.Models.Room]::new()
    $room.RoomCode = 'R1'; $room.Name = 'Room 1'; $room.RoomTypeId = $type.RoomTypeId
    $room.IsActive = $true; $room.CreatedAt = $day.AddDays(-1)
    [void]$db.Rooms.Add($room); [void]$db.SaveChanges()
    $reservation = [MusicBoxManagement.Models.Reservation]::new()
    $reservation.RoomId = $room.RoomId; $reservation.CustomerId = $customer.CustomerId
    $reservation.StartTime = $day.AddHours(9); $reservation.EndTime = $day.AddHours(10)
    $reservation.Status = 'Confirmed'; $reservation.CreatedAt = $day.AddDays(-1)
    [void]$db.Reservations.Add($reservation); [void]$db.SaveChanges()
    $session = [MusicBoxManagement.Models.RoomSession]::new()
    $session.RoomId = $room.RoomId; $session.CustomerId = $customer.CustomerId
    $session.Status = 'Completed'; $session.ActualStartTime = $day.AddHours(10)
    $session.ActualEndTime = $day.AddHours(11); $session.HourlyRate = 120000
    $session.RoomCodeSnapshot = 'R1'; $session.RoomTypeCodeSnapshot = 'STANDARD'; $session.RoomTypeNameSnapshot = 'Standard'
    [void]$db.RoomSessions.Add($session); [void]$db.SaveChanges()
    $order = [MusicBoxManagement.Models.Order]::new()
    $order.RoomSessionId = $session.RoomSessionId; $order.Status = 'Completed'; $order.CreatedAt = $day.AddHours(10)
    $line = [MusicBoxManagement.Models.OrderItem]::new()
    $line.ServiceId = $serviceItem.ServiceId; $line.ServiceNameSnapshot = 'Water'
    $line.Quantity = 2; $line.UnitPrice = 20000
    [void]$order.Items.Add($line); [void]$db.Orders.Add($order)
    $secondOrder = [MusicBoxManagement.Models.Order]::new()
    $secondOrder.RoomSessionId = $session.RoomSessionId; $secondOrder.Status = 'Completed'; $secondOrder.CreatedAt = $day.AddHours(10).AddMinutes(30)
    $renamedLine = [MusicBoxManagement.Models.OrderItem]::new()
    $renamedLine.ServiceId = $serviceItem.ServiceId; $renamedLine.ServiceNameSnapshot = 'Water New'
    $renamedLine.Quantity = 1; $renamedLine.UnitPrice = 20000
    [void]$secondOrder.Items.Add($renamedLine); [void]$db.Orders.Add($secondOrder)
    $invoice = [MusicBoxManagement.Models.Invoice]::new()
    $invoice.InvoiceNumber = 'REPORT-TEST'; $invoice.RoomSessionId = $session.RoomSessionId
    $invoice.RoomCharge = 120000; $invoice.ServiceCharge = 60000; $invoice.TotalAmount = 180000
    $invoice.PaymentMethod = 'Cash'; $invoice.ProcessedByUserId = $staff.Id
    $invoice.ProcessedByNameSnapshot = 'Staff'; $invoice.PaidAt = $day.AddHours(11)
    [void]$db.Invoices.Add($invoice); [void]$db.SaveChanges()
    $clock = [ReportTestClock]::new(); $clock.UtcNow = $day.AddDays(1).AddHours(1)
    $reportService = [MusicBoxManagement.Services.ReportService]::new($db, $clock)
    $query = [MusicBoxManagement.Models.ReportQuery]::new()
    $query.Start = $day; $query.End = $day.AddDays(1)
    $query.Kind = 'revenue-day'; $revenue = $reportService.GetReport($query)
    Assert-True ($revenue.Total.Cells[4].Number -eq 180000 -and $revenue.Total.Cells[1].Number -eq 1) 'Revenue must use paid invoice.'
    $query.Kind = 'revenue-roomtype'; $byType = $reportService.GetReport($query)
    Assert-True ($byType.Rows.Count -eq 1 -and $byType.Rows[0].Cells[0].Display -eq 'STANDARD') 'Room type snapshot was not used.'
    $query.Kind = 'room-usage'; $usage = $reportService.GetReport($query)
    Assert-True ($usage.Total.Cells[1].Number -eq 1) 'Completed room usage wrong.'
    $query.Kind = 'booking-status'; $bookings = $reportService.GetReport($query)
    Assert-True ($bookings.Rows[4].Cells[1].Number -eq 1) 'Expired Confirmed booking must appear as NoShow.'
    $query.Kind = 'service-sales'; $sales = $reportService.GetReport($query)
    Assert-True ($sales.Total.Cells[3].Number -eq 60000 -and $sales.Rows[0].Cells[1].Display.Contains('Water New')) 'Service revenue must include inactive and renamed catalog history.'
    $query.Kind = 'utilization'; $utilization = $reportService.GetReport($query)
    Assert-True ($utilization.Rows[0].Cells[1].Number -eq 60 -and $utilization.Rows[0].Cells[2].Number -eq 780) 'Standard opening utilization wrong.'
    Write-Output 'PASS Report revenue, RoomType snapshot, usage, NoShow, inactive service and utilization on SQL Server'
} finally {
    $db.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
