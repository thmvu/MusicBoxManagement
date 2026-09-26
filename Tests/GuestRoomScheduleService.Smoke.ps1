param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
$project = (Resolve-Path "$PSScriptRoot\..\MusicBoxManagement").Path
[xml]$webConfig = Get-Content "$project\Web.config"
$connection = $webConfig.configuration.connectionStrings.add | Where-Object { $_.name -eq 'DefaultConnection' }
$connectionString = $connection.connectionString.Replace('|DataDirectory|', "$project\App_Data")
$db = [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString)
try {
    $room = $db.Rooms.Find(1)
    if ($room -and $room.IsActive) {
        $localDate = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(7)).Date.AddDays(1)
        $service = [MusicBoxManagement.Services.GuestRoomScheduleService]::new($db, [MusicBoxManagement.Services.SystemClock]::new())
        $slots = $service.GetDay($room.RoomId, $localDate)
        if ($slots.Count -ne 26) { throw "Expected 26 slots, got $($slots.Count)." }
        Write-Output 'PASS EF reads one-room day schedule'
    }
}
finally { $db.Dispose() }
