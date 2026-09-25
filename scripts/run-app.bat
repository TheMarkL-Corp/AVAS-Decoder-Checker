@echo off
echo ==========================================================
echo    Starting AVAS SDVoE Decoder Stream Checker (.NET 8.0)
echo ==========================================================
cd /d "%~dp0\.."
dotnet run --project src\AVASDecoderChecker -c Release
pause
