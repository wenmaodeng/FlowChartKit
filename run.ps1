# 运行 FlowChartKit.Demo
# 用法: .\run.ps1              运行 Demo
#      .\run.ps1 -Build        仅构建不运行
#      .\run.ps1 -Clean        清理后构建并运行
param(
    [switch]$Build,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Clean) {
    Write-Host '清理构建产物...' -ForegroundColor Cyan
    dotnet clean FlowChartKit.slnx --nologo --verbosity quiet | Out-Null
}

if ($Build) {
    Write-Host '构建解决方案...' -ForegroundColor Cyan
    dotnet build FlowChartKit.slnx --nologo
    if ($LASTEXITCODE -ne 0) { Write-Host '构建失败' -ForegroundColor Red; exit 1 }
    Write-Host '构建成功' -ForegroundColor Green
    return
}

Write-Host '启动 FlowChartKit.Demo...' -ForegroundColor Green
dotnet run --project FlowChartKit.Demo --nologo
