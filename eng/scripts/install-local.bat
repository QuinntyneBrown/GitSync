@echo off
setlocal enableextensions

:: ---------------------------------------------------------------------------
:: install-local.bat
:: Rebuilds GitSync and reinstalls it as a .NET global tool from local source.
:: Run from any directory — the script always resolves to the repo root.
:: ---------------------------------------------------------------------------

set PACKAGE_ID=GitSync
set TOOL_CMD=gitsync
set ARTIFACTS_DIR=artifacts

:: Resolve repo root (two directories up from eng\scripts\)
pushd "%~dp0..\.."
set REPO_ROOT=%CD%
echo.
echo [GitSync] Repo root : %REPO_ROOT%
echo.

:: ---------------------------------------------------------------------------
:: 1. Clean the local artifacts output directory
:: ---------------------------------------------------------------------------
echo [1/4] Cleaning artifacts directory...
if exist "%ARTIFACTS_DIR%" (
    rmdir /s /q "%ARTIFACTS_DIR%"
)
mkdir "%ARTIFACTS_DIR%"

:: ---------------------------------------------------------------------------
:: 2. Build in Release
:: ---------------------------------------------------------------------------
echo [2/4] Building (Release)...
echo.
dotnet build "%REPO_ROOT%\src\GitSync\GitSync.csproj" -c Release --nologo
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed. Fix compilation errors before reinstalling.
    popd & exit /b 1
)

:: ---------------------------------------------------------------------------
:: 3. Pack
:: ---------------------------------------------------------------------------
echo.
echo [3/4] Packing NuGet tool...
echo.
dotnet pack "%REPO_ROOT%\src\GitSync\GitSync.csproj" -c Release -o "%ARTIFACTS_DIR%" --no-build --nologo
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Pack failed.
    popd & exit /b 1
)

:: ---------------------------------------------------------------------------
:: 4. Uninstall existing global tool (ignore exit code — may not be installed)
::    then install from the freshly built package.
:: ---------------------------------------------------------------------------
echo.
echo [4/4] Reinstalling global tool...
echo.
dotnet tool uninstall -g %PACKAGE_ID% >nul 2>&1

dotnet tool install -g %PACKAGE_ID% --add-source "%ARTIFACTS_DIR%" --no-cache
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Install failed.
    popd & exit /b 1
)

:: ---------------------------------------------------------------------------
:: Done
:: ---------------------------------------------------------------------------
echo.
echo [GitSync] Installation complete.
echo.
%TOOL_CMD% --version
echo.

popd
endlocal
