@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ==========================================
echo   Peter Minecraft launcher - build script
echo   version 1.1.4
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
echo 编译器: %CSC%

if not exist "dist" mkdir "dist"
if not exist "assets\icon.ico" (
    echo [WARN] assets\icon.ico 不存在，将不带图标编译
    set "ICONOPT="
) else (
    set "ICONOPT=/win32icon:assets\icon.ico"
)

rem ---- 收集内置 JRE 资源：assets\jreX.zip 全部打进 exe，运行时自动释放 ----
set "RESOPT="
set "JRECOUNT=0"
for %%V in (8 17 21 25) do (
    if exist "assets\jre%%V.zip" (
        set "RESOPT=!RESOPT! /resource:assets\jre%%V.zip,PeterLauncher.Resources.jre%%V.zip"
        set /a JRECOUNT+=1
    ) else (
        echo [WARN] assets\jre%%V.zip 不存在，该版本 JRE 不会内置进 exe
    )
)
echo 内置 JRE 资源: !JRECOUNT! 个
echo.
echo 开始编译...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
 /out:"dist\PeterMinecraftLauncher.exe" !ICONOPT! !RESOPT! ^
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
echo 发布：
echo   单文件分发：直接把 dist\PeterMinecraftLauncher.exe 拷走即可。
echo   首次启动自动从 exe 内置资源释放 JRE\jre8 / jre17 / jre21 / jre25（已存在则跳过）。
echo   再配上 config.ini 和 Games\.minecraft 就能用。
echo.
pause
