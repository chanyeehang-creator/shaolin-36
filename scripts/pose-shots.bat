@echo off
rem Pose pictures: renders the stick figure's poses (game\data\poses.csv) into the pose-shots folder, then opens it.
rem Each picture shows one pose three ways: as you see an opponent, from its right, from its left.
rem   scripts\pose-shots.bat                                      every pose
rem   scripts\pose-shots.bat luohan_quan_1.windup luohan_quan_1.strike   just these
setlocal
cd /d "%~dp0.."
if "%GODOT%"=="" set GODOT=godot
dotnet build game\Shaolin36.csproj -v quiet -nologo || goto :fail
"%GODOT%" --path game -- --pose-shots "%CD%\pose-shots" %*
start "" "%CD%\pose-shots"
goto :eof
:fail
echo Build failed. Run scripts\test.bat to see the errors.
pause
exit /b 1
