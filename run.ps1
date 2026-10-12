# FlowChartKit 启动脚本
# 用法: powershell -ExecutionPolicy Bypass -File run.ps1
# 可选参数: -BuildOnly (只编译不运行)

param(
    [switch]$BuildOnly
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root "LLT.slnx"
$project = Join-Path $root "FlowChartKit.Demo\FlowChartKit.Demo.csproj"

Write-Host "=== FlowChartKit 启动 ===" -ForegroundColor Cyan
Write-Host "解决方案: $solution"
Write-Host ""

# 编译
Write-Host "[1/2] 编译解决方案..." -ForegroundColor Yellow
dotnet build $solution -c Debug 2>&1 | Select-Object -Last 3
if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败！" -ForegroundColor Red
    exit 1
}
Write-Host "编译成功。" -ForegroundColor Green
Write-Host ""

if ($BuildOnly) {
    Write-Host "已跳过运行（-BuildOnly）。" -ForegroundColor Cyan
    exit 0
}

# 运行
Write-Host "[2/2] 启动 FlowChartKit.Demo..." -ForegroundColor Yellow
dotnet run --project $project -c Debug --no-build
