@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ==========================================
echo   Peter Minecraft launcher - build script
echo   version 1.1.2
echo ==========================================
echo.

rem ---- 找编译器：优先 64 位，其次 32 位 ----
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] 找不到 csc.exe，本机没有安装 .NET Framework 4.x
    pause
    exit /b 1
)
echo [1/2] 编译器: %CSC%

if not exist "dist" mkdir "dist"
if not exist "assets\icon.ico" (
    echo [WARN] assets\icon.ico 不存在，将不带图标编译
    set "ICONOPT="
) else (
    set "ICONOPT=/win32icon:\"assets\icon.ico\""
)

echo [2/2] 开始编译...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
 /out:"dist\PeterMinecraftLauncher.exe" !ICONOPT! ^
 /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
 /recurse:src\*.cs

if errorlevel 1 (
    echo.
    echo [FAILED] 编译失败，请把上面的错误发出来
    pause
    exit /b 1
)

echo.
echo [OK] 编译完成: dist\PeterMinecraftLauncher.exe
echo.
echo 发布时把这些东西一起放进 U 盘根目录:
echo   PeterMinecraftLauncher.exe   (来自 dist)
echo   config.ini
echo   Games\.minecraft
echo   Java\jre8                    (可选，离线兜底的便携 JRE 8)
echo   data\minecraft.zip           (可选，首次启动自动释放用)
echo.
echo 注意：1.1.1 起不再需要 Java 安装包，Java 由启动器按游戏版本自动下载。
echo.
pause
