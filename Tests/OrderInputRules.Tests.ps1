param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
function Line($serviceId, $quantity) {
    $line = [MusicBoxManagement.Services.OrderLineInput]::new()
    $line.ServiceId = $serviceId; $line.Quantity = $quantity
    return $line
}
function Check($lines) {
    $typed = [MusicBoxManagement.Services.OrderLineInput[]]@($lines)
    return [MusicBoxManagement.Services.OrderInputRules]::Validate($typed)
}
$valid = Check @((Line 1 4), (Line 1 6), (Line 2 1))
Assert-True $valid.IsValid 'Merged valid lines rejected.'
Assert-True ($valid.Lines.Count -eq 2 -and $valid.Lines[0].Quantity -eq 10) 'Duplicate Service was not merged.'
Assert-True (!(Check @((Line 1 6), (Line 1 5))).IsValid) 'Merged quantity over 10 accepted.'
Assert-True (!(Check @((Line 1 0))).IsValid) 'Zero quantity accepted.'
Assert-True (!(Check @((Line 0 1))).IsValid) 'Invalid ServiceId accepted.'
Assert-True (!(Check @()).IsValid) 'Empty order accepted.'
Write-Output 'PASS merge duplicate Service before quantity validation'
