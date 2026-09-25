param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll")

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path

function Assert-Input($name, $fullName, $phone, $valid, $expectedPhone) {
    $result = [MusicBoxManagement.Services.ReservationInputRules]::Validate($fullName, $phone)
    if ($result.IsValid -ne $valid) { throw "$name : expected $valid; $($result.Error)" }
    if ($valid -and $result.PhoneNumber -ne $expectedPhone) { throw "$name : wrong normalized phone" }
    Write-Output "PASS $name"
}

Assert-Input 'Ten tieng Viet va SĐT +84' '  Nguyễn Văn A  ' '+84 912-345-678' $true '0912345678'
Assert-Input 'Khong co ten' '   ' '0912345678' $false $null
Assert-Input 'Ten qua 100 ky tu' ('A' * 101) '0912345678' $false $null
Assert-Input 'SĐT sai' 'Nguyễn Văn A' '12345' $false $null
