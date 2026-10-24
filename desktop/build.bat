@echo off
set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    echo [Error] C# Compiler not found: %CSC_PATH%
    pause
    exit /b 1
)

echo [FolderSnap Desktop] Compiling native Windows client...
"%CSC_PATH%" /target:winexe /platform:x64 /optimize+ /out:"%~dp0FolderSnapDesktop.exe" "%~dp0Interop\ShellCom.cs" "%~dp0Interop\Win32Native.cs" "%~dp0Core\ExplorerHostControl.cs" "%~dp0Core\ScrollStitcher.cs" "%~dp0Core\BatchScanner.cs" "%~dp0UI\AboutDialog.cs" "%~dp0UI\MainForm.cs" "%~dp0Program.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo =========================================================
    echo [Success] FolderSnapDesktop.exe compiled successfully!
    echo Output: %~dp0FolderSnapDesktop.exe
    echo =========================================================
    echo.
) else (
    echo.
    echo [Failed] Compilation errors occurred.
    pause
)
