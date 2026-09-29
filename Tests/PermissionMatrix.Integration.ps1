#requires -PSEdition Desktop
param([string]$AssemblyPath = "$PSScriptRoot\..\MusicBoxManagement\bin\MusicBoxManagement.dll", [string]$SqlServer = '.')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $AssemblyPath).Path
function Assert-True($condition, $message) { if (!$condition) { throw $message } }
$databaseName = 'MusicBoxPermissionTest_' + [Guid]::NewGuid().ToString('N')
$connectionString = "Data Source=$SqlServer;Initial Catalog=$databaseName;Integrated Security=True"
function New-Context { [MusicBoxManagement.Models.ApplicationDbContext]::new($connectionString) }
$db = New-Context
try {
    $db.Database.Create()
    $staffRole = [Microsoft.AspNet.Identity.EntityFramework.IdentityRole]::new('Staff')
    $managerRole = [Microsoft.AspNet.Identity.EntityFramework.IdentityRole]::new('Manager')
    $adminRole = [Microsoft.AspNet.Identity.EntityFramework.IdentityRole]::new('Admin')
    [void]$db.Roles.Add($staffRole); [void]$db.Roles.Add($managerRole); [void]$db.Roles.Add($adminRole)
    foreach ($code in [MusicBoxManagement.Services.PermissionCodes]::All) {
        $permission = [MusicBoxManagement.Models.Permission]::new()
        $permission.Code = $code; $permission.Name = $code
        [void]$db.Permissions.Add($permission)
    }
    $actor = [MusicBoxManagement.Models.ApplicationUser]::new()
    $actor.Id = [Guid]::NewGuid().ToString('N'); $actor.UserName = 'matrix_admin'; $actor.FullName = 'Admin'
    [void]$db.Users.Add($actor)
    $staff = [MusicBoxManagement.Models.ApplicationUser]::new()
    $staff.Id = [Guid]::NewGuid().ToString('N'); $staff.UserName = 'matrix_staff'; $staff.FullName = 'Staff'
    [void]$db.Users.Add($staff)
    [void]$db.SaveChanges()
    $staffRoleLink = [Microsoft.AspNet.Identity.EntityFramework.IdentityUserRole]::new()
    $staffRoleLink.UserId = $staff.Id; $staffRoleLink.RoleId = $staffRole.Id
    [void]$staff.Roles.Add($staffRoleLink)
    $adminRoleLink = [Microsoft.AspNet.Identity.EntityFramework.IdentityUserRole]::new()
    $adminRoleLink.UserId = $actor.Id; $adminRoleLink.RoleId = $adminRole.Id
    [void]$actor.Roles.Add($adminRoleLink)
    [void]$db.SaveChanges()
    $service = [MusicBoxManagement.Services.PermissionMatrixService]::new($db)
    Assert-True (!$service.SetGrant('Staff', 'Room.Manage', $true, $staff.Id).Succeeded) 'Staff edited role grants.'
    $result = $service.SetGrant('Staff', 'Room.Manage', $true, $actor.Id)
    Assert-True $result.Succeeded 'Staff business permission was not granted.'
    $fresh = New-Context
    try {
        Assert-True (([MusicBoxManagement.Services.PermissionService]::new($fresh)).HasPermission($staff.Id, 'Room.Manage')) 'Grant not effective in next request.'
    } finally { $fresh.Dispose() }
    Assert-True (!$service.SetGrant('Staff', 'Report.View', $true, $actor.Id).Succeeded) 'Staff reporting was granted.'
    Assert-True (!$service.SetGrant('Manager', 'User.Manage', $true, $actor.Id).Succeeded) 'Admin-only permission was granted.'
    Assert-True (!$service.SetGrant('Admin', 'Room.Manage', $false, $actor.Id).Succeeded) 'Admin permission was editable.'
    Assert-True ($service.SetGrant('Staff', 'Room.Manage', $true, $actor.Id).Succeeded) 'Repeat grant failed.'
    Assert-True (@($db.AuditLogs).Count -eq 1) 'Repeat grant created extra audit.'
    Assert-True ($service.SetGrant('Staff', 'Room.Manage', $false, $actor.Id).Succeeded) 'Revoke failed.'
    $check = New-Context
    try {
        Assert-True (!([MusicBoxManagement.Services.PermissionService]::new($check)).HasPermission($staff.Id, 'Room.Manage')) 'Revoke not effective in next request.'
        Assert-True (@($check.AuditLogs).Count -eq 2) 'Grant and revoke were not audited once each.'
        Write-Output 'PASS Permission matrix grant/revoke, invariant limits, next-request effect and audit on SQL Server'
    } finally { $check.Dispose() }
} finally {
    $db.Dispose()
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    $cleanup = New-Context
    try { if ($cleanup.Database.Exists()) { [void]$cleanup.Database.Delete() } }
    finally { $cleanup.Dispose() }
}
