# 编译 NetMeter.exe
# 依赖：Windows 自带 .NET Framework 4.x（无需安装任何 SDK）
# 用法：右键「使用 PowerShell 运行」，或在终端执行  powershell -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw '找不到 C# 编译器 csc.exe，请确认系统已安装 .NET Framework 4.x'
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src\NetMeter.cs'
$out  = Join-Path $root 'NetMeter.exe'

Write-Host "编译器: $csc"
Write-Host "源码  : $src"

& $csc /nologo /target:winexe /optimize+ /out:"$out" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "$src"

if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

$f = Get-Item $out
Write-Host ""
Write-Host ("编译完成: {0}  ({1:N0} 字节)" -f $f.FullName, $f.Length)
Write-Host "直接双击运行即可，程序会缩到右下角托盘。"
