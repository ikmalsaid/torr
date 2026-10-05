@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   Building Torr using csc.exe
echo =======================================================

:: 1. Search for csc.exe in standard .NET Framework locations
set "CSC="
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    goto :Found
)
if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    goto :Found
)

:: 2. Check PATH
for %%X in (csc.exe) do (
    set "FOUND=%%~$PATH:X"
    if defined FOUND (
        set "CSC=%%~$PATH:X"
        goto :Found
    )
)

echo [ERROR] csc.exe could not be found!
echo Please ensure .NET Framework 4.0 or later is installed.
exit /b 1

:Found
echo Found C# Compiler:
echo   "%CSC%"
echo.
echo Compiling source files from src...

if not exist "bin" mkdir "bin"

"%CSC%" /nologo /target:winexe /optimize+ /out:bin\torr.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo =======================================================
    echo   BUILD SUCCESSFUL!
    echo   Output: bin\torr.exe
    echo =======================================================
) else (
    echo.
    echo [ERROR] Compilation failed with error code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)

endlocal
