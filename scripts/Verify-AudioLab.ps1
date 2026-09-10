param([string]$SongsDirectory, [string]$OutputDirectory, [string]$CacheDirectory, [switch]$ExpectCache)
$ErrorActionPreference = 'Stop'
$pulseRoot = Split-Path -Parent $PSScriptRoot
if (-not $SongsDirectory) { $SongsDirectory = Join-Path $pulseRoot 'TestResults\LabSongs' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $pulseRoot 'TestResults\AudioLab' }
if (-not $CacheDirectory) { $CacheDirectory = Join-Path $pulseRoot 'TestResults\LabCache' }
$SongsDirectory = [IO.Path]::GetFullPath($SongsDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
if (-not (Test-Path -LiteralPath $SongsDirectory)) { throw 'Prepare WAV and MP3 fixtures first; see docs/AUDIO_LAB.md.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$pulseExecutable = Join-Path $pulseRoot 'Builds\Windows\Project Pulse.exe'
$pulseArguments = '-pulse-lab-verify -pulse-windowed -pulse-songs "' + $SongsDirectory + '" -pulse-lab-cache "' + $CacheDirectory + '" -pulse-output "' + $OutputDirectory + '" -logFile "' + (Join-Path $OutputDirectory 'player.log') + '"'
if ($ExpectCache) { $pulseArguments += ' -pulse-expect-cache' }
$pulseProcess = Start-Process -FilePath $pulseExecutable -ArgumentList $pulseArguments -WorkingDirectory $pulseRoot -WindowStyle Hidden -PassThru
Write-Output "Audio lab verification started (PID $($pulseProcess.Id)). Results: $OutputDirectory"
$pulseProcess.WaitForExit()
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'verification.txt')) { Get-Content -LiteralPath (Join-Path $OutputDirectory 'verification.txt') }
if ($pulseProcess.ExitCode -ne 0) { throw "Audio lab verification failed with exit code $($pulseProcess.ExitCode). Inspect player.log." }
