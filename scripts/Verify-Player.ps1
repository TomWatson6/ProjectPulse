param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$pulseRoot = Split-Path -Parent $PSScriptRoot
$pulseExecutable = Join-Path $pulseRoot 'Builds\Windows\Project Pulse.exe'
if (-not (Test-Path -LiteralPath $pulseExecutable)) { throw 'Build the Windows player first: Project Pulse > Build Windows player in Unity.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $pulseRoot 'TestResults\Player' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$pulseArguments = '-pulse-verify -pulse-output "' + $OutputDirectory + '" -logFile "' + (Join-Path $OutputDirectory 'player.log') + '"'
$pulseProcess = Start-Process -FilePath $pulseExecutable -ArgumentList $pulseArguments -WorkingDirectory $pulseRoot -WindowStyle Hidden -PassThru
Write-Output "Player verification started (PID $($pulseProcess.Id)). Results: $OutputDirectory"
$pulseProcess.WaitForExit()
if ($pulseProcess.ExitCode -ne 0) { throw "Player verification failed with exit code $($pulseProcess.ExitCode). Inspect player.log and verification.txt." }
Get-Content -LiteralPath (Join-Path $OutputDirectory 'verification.txt')
