<#
.SYNOPSIS
    Launches the AVAS SDVoE Decoder Stream Checker application.
#>

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Join-Path (Split-Path -Parent $ScriptDir) "src\AVASDecoderChecker"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Starting AVAS SDVoE Decoder Stream Checker (.NET 8.0)  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

dotnet run --project "$ProjectDir" -c Release
