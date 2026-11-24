@echo off
rem One-click Play: builds the C# code and runs the game.
rem Set GODOT to your Godot 4.7 .NET executable if it is not on PATH, e.g.
rem   setx GODOT "C:\Godot\Godot_v4.7.2-stable_mono_win64.exe"
setlocal
cd /d "%~dp0.."
if "%GODOT%"=="" set GODOT=godot
dotnet build game\Shaolin36.csproj -v quiet -nologo || goto :fail
"%GODOT%" --path game
goto :eof
:fail
echo Build failed. Run scripts\test.bat to see the errors.
pause
exit /b 1
