@echo off
rem Runs every check Claude Code runs: data check and the core rule tests.
setlocal
cd /d "%~dp0.."
dotnet run --project tools\DataCheck -- game\data\techniques.csv || goto :fail
dotnet test Shaolin36.sln -nologo || goto :fail
echo.
echo All checks passed.
pause
exit /b 0
:fail
echo.
echo A check failed. See above.
pause
exit /b 1
