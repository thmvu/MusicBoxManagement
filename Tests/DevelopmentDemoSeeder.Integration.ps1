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
    [void]$db.Roles.Add([Microsoft.AspNet.Identity.EntityFramework.IdentityRole]::new('Staff'))
    [void]$db.Roles.Add([Microsoft.AspNet.Identity.EntityFramework.IdentityRole]::new('Manager'))
    [void]$db.SaveChanges()
    $assembly = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path)
    $type = $assembly.GetType('MusicBoxManagement.Services.DevelopmentDemoSeeder')
    Assert-True ($type -ne $null) 'Development demo seeder is missing.'
    $method = $type.GetMethod('Seed')
    $now = [DateTimeOffset]::UtcNow
    $password = 'MusicboxDemo!2026'
    try { [void]$method.Invoke($null, @($db, $now, $password)) } catch { throw $_.Exception.ToString() }
    [void]$method.Invoke($null, @($db, $now.AddHours(1), $password))
    $users = [Microsoft.AspNet.Identity.UserManager[MusicBoxManagement.Models.ApplicationUser]]::new(
        [Microsoft.AspNet.Identity.EntityFramework.UserStore[MusicBoxManagement.Models.ApplicationUser]]::new($db))
    $staff = $users.FindByNameAsync('demo.staff').GetAwaiter().GetResult()
    $manager = $users.FindByNameAsync('demo.manager').GetAwaiter().GetResult()
    Assert-True ($staff -ne $null -and $users.IsInRoleAsync($staff.Id, 'Staff').GetAwaiter().GetResult()) 'Demo Staff identity account is missing.'
    Assert-True ($manager -ne $null -and $users.IsInRoleAsync($manager.Id, 'Manager').GetAwaiter().GetResult()) 'Demo Manager identity account is missing.'
    Assert-True ($users.CheckPasswordAsync($staff, $password).GetAwaiter().GetResult()) 'Demo password was not stored through ASP.NET Identity.'
    Assert-True (@($db.Users).Count -eq 2) 'Demo identity accounts were duplicated.'
    Assert-True (@($db.Rooms).Count -eq 3) 'Demo rooms were not created idempotently.'
    Assert-True (@($db.Rooms | Where-Object { $_.IsActive -and $_.ImageUrl -eq '~/Content/images/landing-room.png' }).Count -eq 2) 'Active demo rooms are missing their room image.'
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
