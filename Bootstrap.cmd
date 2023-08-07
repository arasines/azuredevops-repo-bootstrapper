@ECHO OFF
SETLOCAL EnableDelayedExpansion
for /F "tokens=1,2 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do rem"') do (
  set "DEL=%%a"
)
SET alwaysCopy=false
SET projectName=""
SET projectDisplayName=default
echo %1
echo %2
IF "%1"=="--help" GOTO Help
IF "%1"=="-force" SET alwaysCopy=true
IF NOT "%1"=="" SET projectName=%1
IF NOT "%1"=="" SET projectDisplayName=%1
IF "%2"=="-force" SET alwaysCopy=true
IF NOT "%2"=="" SET projectName=%2
IF NOT "%2"=="" SET projectDisplayName=%2
GOTO Execution  

:Help
echo.
echo usage: %~nx0 [--help] [-force] [project_name]
GOTO:EOF

:Execution
CLS
echo.
echo.
call :colorEcho 0d "Welcome to the Azure DevOps Repo Bootstrap tool"
echo.
echo.
echo These are the %~nx0 execution parameters:
echo   Project Name: %projectDisplayName%
echo   Force Overwrite: %alwaysCopy%
echo.
SET /P AREYOUSURE=Are you sure you want to continue(y/[N])?
IF /I "%AREYOUSURE%" NEQ "y" GOTO goodbye
echo.
call :colorEcho 0d "Starting execution of %~nx0. Please wait.."
echo.
dotnet new tool-manifest --force
dotnet tool install Nake --version 3.0.0-beta-01
dotnet tool restore
@echo off
rmdir .\.bootstrap\files\AppBootstrap /s /q
git clone https://<AZURE_DEVOPS_REPO_URL> .\.bootstrap\files\AppBootstrap

dotnet nake -f "Bootstrap.csx" Run alwaysCopy=%alwaysCopy% projectName=%projectName%
GOTO:EOF
:goodbye
echo.
call :colorEcho 0e "Goodbye"
echo.
GOTO:EOF
:colorEcho
echo off
<nul set /p ".=%DEL%" > "%~2"
findstr /v /a:%1 /R "^$" "%~2" nul
del "%~2" > nul 2>&1i