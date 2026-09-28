#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll", [string]$SqlServer = '.')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$databaseName = 'MusicBoxHistoryTest_' + [Guid]::NewGuid().ToString('N')
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
    $customer.FullName = 'Old Name'; $customer.PhoneNumber = '0912345678'
    [void]$db.Customers.Add($customer)
    $newCustomer = [MusicBoxManagement.Models.Customer]::new()
    $newCustomer.FullName = 'No Visits'; $newCustomer.PhoneNumber = '0912345679'
    [void]$db.Customers.Add($newCustomer)
    $staff = [MusicBoxManagement.Models.ApplicationUser]::new()
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'history_test_staff'; $staff.FullName = 'Staff'
    [void]$db.Users.Add($staff)
    [void]$db.SaveChanges()
    $room = [MusicBoxManagement.Models.Room]::new()
    $room.RoomCode = 'R1'; $room.Name = 'Room 1'; $room.RoomTypeId = $type.RoomTypeId
    $room.IsActive = $true; $room.CreatedAt = [DateTimeOffset]::UtcNow
    [void]$db.Rooms.Add($room); [void]$db.SaveChanges()
    $offset = [TimeSpan]::FromHours(7)
    $start = [DateTimeOffset]::new(([DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(-1).AddHours(10)), $offset).ToUniversalTime()
    $reservation = [MusicBoxManagement.Models.Reservation]::new()
    $reservation.RoomId = $room.RoomId; $reservation.CustomerId = $customer.CustomerId
    $reservation.StartTime = $start; $reservation.EndTime = $start.AddHours(1)
    $reservation.Status = 'Completed'; $reservation.CreatedAt = $start.AddDays(-1)
    [void]$db.Reservations.Add($reservation); [void]$db.SaveChanges()
    $session = [MusicBoxManagement.Models.RoomSession]::new()
    $session.RoomId = $room.RoomId; $session.CustomerId = $customer.CustomerId
    $session.ReservationId = $reservation.ReservationId; $session.Status = 'Completed'
    $session.ActualStartTime = $start; $session.ActualEndTime = $start.AddHours(1)
    $session.HourlyRate = 120000; $session.RoomCodeSnapshot = 'R1'
    $session.RoomTypeCodeSnapshot = 'STANDARD'; $session.RoomTypeNameSnapshot = 'Standard'
    [void]$db.RoomSessions.Add($session); [void]$db.SaveChanges()
    $invoice = [MusicBoxManagement.Models.Invoice]::new()
    $invoice.InvoiceNumber = 'TEST-HISTORY'; $invoice.RoomSessionId = $session.RoomSessionId
    $invoice.RoomCharge = 120000; $invoice.ServiceCharge = 0; $invoice.TotalAmount = 120000
    $invoice.PaymentMethod = 'Cash'; $invoice.ProcessedByUserId = $staff.Id
    $invoice.ProcessedByNameSnapshot = 'Staff'; $invoice.PaidAt = $start.AddHours(1)
    [void]$db.Invoices.Add($invoice); [void]$db.SaveChanges()
    $customer.FullName = 'New Name'; $customer.PhoneNumber = '0987654321'
    [void]$db.SaveChanges()
    $reader = New-Context
    try {
        $service = [MusicBoxManagement.Services.CustomerHistoryService]::new($reader)
        $history = $service.GetDetails($customer.CustomerId, $true, $true, $true)
        Assert-True ($history.FullName -eq 'New Name' -and $history.PhoneNumber -eq '0987654321') 'Current customer details wrong.'
        Assert-True ($history.CompletedSessionCount -eq 1 -and $history.LastUsedAt -eq $start) 'Visit summary wrong.'
        Assert-True ($history.Reservations.Count -eq 1 -and $history.Sessions.Count -eq 1 -and $history.Invoices.Count -eq 1) 'History lost after phone change.'
        $limited = $service.GetDetails($customer.CustomerId, $false, $false, $false)
        Assert-True ($limited.Reservations.Count -eq 0 -and $limited.Sessions.Count -eq 0 -and $limited.Invoices.Count -eq 0) 'Restricted history was loaded.'
        $empty = $service.GetDetails($newCustomer.CustomerId, $true, $true, $true)
        Assert-True ($empty.CompletedSessionCount -eq 0 -and $empty.LastUsedAt -eq $null) 'New customer summary wrong.'
        Write-Output 'PASS Customer history remains linked by ID after phone change, and honors section permissions'
    } finally { $reader.Dispose() }
} finally {
    $db.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
