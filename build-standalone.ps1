# ============================================================
# 构建为“独立可执行程序”（自包含单文件，无需安装 .NET 即可运行）
# 用法：右键 -> 使用 PowerShell 运行，或在终端执行：
#     powershell -ExecutionPolicy Bypass -File .\build-standalone.ps1
# 输出：.\publish\win-x64\EZRecoil.exe（旁边带有 JsonRAW 文件夹）
# ============================================================
$ErrorActionPreference = 'Stop'

Write-Host "正在发布独立可执行程序（win-x64，自包含单文件）..." -ForegroundColor Cyan
dotnet publish .\EZ_Recoil.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -o .\publish\win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "发布失败，退出码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "" -ForegroundColor Cyan
Write-Host "✅ 发布完成！" -ForegroundColor Green
Write-Host "   可执行文件: .\publish\win-x64\EZRecoil.exe"
Write-Host "   直接双击 EZRecoil.exe 即可运行（读取 exe 旁边的 JsonRAW 文件夹）。"
Write-Host "   可把整个 publish\win-x64 文件夹拷贝到其它 Windows 电脑（x64）使用。"
