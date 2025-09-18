# AGI.Captor MSI Package Verification Script
# 验证生成的MSI安装包的基本属性和完整性

param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath
)

Write-Host "🔍 AGI.Captor MSI Package Verification" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# 检查文件是否存在
if (-not (Test-Path $MsiPath)) {
    Write-Host "❌ MSI file not found: $MsiPath" -ForegroundColor Red
    exit 1
}

# 基本文件信息
$fileInfo = Get-Item $MsiPath
Write-Host "📁 File Information:" -ForegroundColor Green
Write-Host "   Name: $($fileInfo.Name)"
Write-Host "   Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB"
Write-Host "   Created: $($fileInfo.CreationTime)"
Write-Host "   Modified: $($fileInfo.LastWriteTime)"
Write-Host ""

# 计算哈希
Write-Host "🔐 File Integrity:" -ForegroundColor Green
$hash = Get-FileHash $MsiPath -Algorithm SHA256
Write-Host "   SHA256: $($hash.Hash)"
Write-Host ""

# 尝试读取MSI属性
Write-Host "📋 MSI Properties:" -ForegroundColor Green
try {
    # 使用Windows Installer COM对象读取MSI属性
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($MsiPath, 0)  # 0 = msiOpenDatabaseModeReadOnly
    
    # 查询属性表
    $view = $database.OpenView("SELECT Property, Value FROM Property")
    $view.Execute()
    
    $properties = @{}
    while ($record = $view.Fetch()) {
        $prop = $record.StringData(1)
        $value = $record.StringData(2)
        $properties[$prop] = $value
    }
    
    # 显示关键属性
    $keyProperties = @('ProductName', 'ProductVersion', 'Manufacturer', 'ProductCode', 'UpgradeCode')
    foreach ($prop in $keyProperties) {
        if ($properties.ContainsKey($prop)) {
            Write-Host "   $prop`: $($properties[$prop])"
        }
    }
    
    $view.Close()
    $database = $null
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($installer) | Out-Null
}
catch {
    Write-Host "   ⚠️  Could not read MSI properties: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ Verification completed!" -ForegroundColor Green
Write-Host ""
Write-Host "💡 Next steps:" -ForegroundColor Yellow
Write-Host "   1. Test installation: Right-click MSI and select 'Install'"
Write-Host "   2. Check Start Menu and Desktop shortcuts"
Write-Host "   3. Verify application launches correctly"
Write-Host "   4. Test uninstallation from Control Panel"