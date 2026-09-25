param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
$project = (Resolve-Path "$PSScriptRoot\..\MusicBoxManagement").Path
[xml]$webConfig = Get-Content "$project\Web.config"
$connection = $webConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq 'DefaultConnection' }
$connectionString = $connection.connectionString.Replace('|DataDirectory|', "$project\App_Data")
$db = [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString)
try {
    $clock = [MusicBoxManagement.Services.SystemClock]::new()
    $service = [MusicBoxManagement.Services.AvailabilityService]::new($db, $clock)
    $offset = [TimeSpan]::FromHours(7)
    $tomorrow = [DateTimeOffset]::UtcNow.ToOffset($offset).Date.AddDays(1).AddHours(9)
    $startUtc = [DateTimeOffset]::new($tomorrow, $offset).ToUniversalTime()
    $missing = $service.CheckReservation(999999, $null, $startUtc, 60)
    if ($missing.IsAvailable -or !$missing.Error) { throw 'Missing room should be rejected.' }
    Write-Output 'PASS Database query rejects missing room'
    $room = $db.Rooms.Find(1)
    if ($room -and $room.IsActive) {
        $result = $service.CheckReservation($room.RoomId, $null, $startUtc, 60)
        if (!$result.IsAvailable -and !$result.Error) { throw 'Rejected interval needs a reason.' }
        if ($result.IsAvailable -and $result.EndTimeUtc -ne $startUtc.AddMinutes(60)) { throw 'Wrong end time.' }
        Write-Output 'PASS EF queries Reservations and RoomSessions for an active room'
    }
}
finally { $db.Dispose() }
