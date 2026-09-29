#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll", [string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$data = [MusicBoxManagement.Models.ReportData]::new()
$data.Title = 'Report Test'; $data.TimeBasis = 'PaidAt'; $data.FilterDescription = '01/09/2026–02/09/2026'
$data.GeneratedAt = [DateTimeOffset]::UtcNow
[void]$data.Headers.Add('Day'); [void]$data.Headers.Add('Revenue')
[void]$data.Rows.Add([MusicBoxManagement.Models.ReportRow]::Of(
    [MusicBoxManagement.Models.ReportCell]::Text('2026-09-01'),
    [MusicBoxManagement.Models.ReportCell]::Amount(120000)))
$data.Total = [MusicBoxManagement.Models.ReportRow]::Of(
    [MusicBoxManagement.Models.ReportCell]::Text('Total'),
    [MusicBoxManagement.Models.ReportCell]::Amount(120000))
$bytes = [MusicBoxManagement.Services.ReportExcelWriter]::Write($data)
if ($OutputPath) { [System.IO.File]::WriteAllBytes($OutputPath, $bytes) }
Assert-True ($bytes.Length -gt 500) 'Excel file is empty.'
$stream = [System.IO.MemoryStream]::new($bytes)
$archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Read)
try {
    $sheet = $archive.GetEntry('xl/worksheets/sheet1.xml')
    Assert-True ($sheet -ne $null) 'Worksheet missing.'
    $reader = [System.IO.StreamReader]::new($sheet.Open())
    try { $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
    Assert-True ($xml.Contains('Report Test') -and $xml.Contains('120000')) 'Title or numeric amount missing.'
    Assert-True (!$xml.Contains('<v>120.000</v>')) 'Money was exported as formatted text.'
    Write-Output 'PASS XLSX package includes metadata, headers, rows and numeric totals'
} finally { $archive.Dispose(); $stream.Dispose() }
