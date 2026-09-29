#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll", [string]$SqlServer = '.')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$databaseName = 'MusicBoxDemoSeedTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=$SqlServer;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
$db = New-Context
try {
    $db.Database.Create()
    $standard = [MusicBoxManagement.Models.RoomType]::new()
    $standard.Code = 'STANDARD'; $standard.Name = 'Standard'; $standard.Capacity = 4; $standard.PricePerHour = 120000; $standard.Amenities = 'TV'
    $vip = [MusicBoxManagement.Models.RoomType]::new()
    $vip.Code = 'VIP'; $vip.Name = 'VIP'; $vip.Capacity = 6; $vip.PricePerHour = 200000; $vip.Amenities = 'Sofa'
    [void]$db.RoomTypes.Add($standard); [void]$db.RoomTypes.Add($vip); [void]$db.SaveChanges()
    $assembly = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path)
    $type = $assembly.GetType('MusicBoxManagement.Services.DevelopmentDemoSeeder')
    Assert-True ($type -ne $null) 'Development demo seeder is missing.'
    $method = $type.GetMethod('Seed')
    $now = [DateTimeOffset]::UtcNow
    try { [void]$method.Invoke($null, @($db, $now)) } catch { throw $_.Exception.ToString() }
    [void]$method.Invoke($null, @($db, $now.AddHours(1)))
    Assert-True (@($db.Rooms).Count -eq 3) 'Demo rooms were not created idempotently.'
    Assert-True (@($db.Rooms | Where-Object { !$_.IsActive -and $_.InactiveReason }).Count -eq 1) 'Inactive demo room is missing its reason.'
    Assert-True (@($db.Customers).Count -eq 2) 'Demo customers were not created idempotently.'
    Assert-True (@($db.Reservations | Where-Object { $_.Status -eq 'Confirmed' }).Count -eq 1) 'Relative demo booking is missing.'
    Assert-True (@($db.RoomSessions | Where-Object { $_.Status -eq 'Active' }).Count -eq 1) 'Relative active demo session is missing.'
    Write-Output 'PASS Development demo seed creates repeatable rooms, customers, booking and active session'
} finally {
    $db.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
