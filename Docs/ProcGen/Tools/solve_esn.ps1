<#
Reference solver for Escult Sketch Notation (ESN) levels — wrapper.

The solver logic lives in solve_esn.py (same directory); this script only
locates a Python 3 interpreter and forwards the arguments, so existing callers
keep working unchanged. Output schema is identical to the old pure-PowerShell
implementation (verified byte-identical on the levels in Assets/ProcGen/Levels/).

Usage:
  powershell -NoProfile -File solve_esn.ps1 -EsnPath <file> [-MaxCost 30] [-MaxStates 300000]
#>
param(
  [Parameter(Mandatory=$true)][string]$EsnPath,
  [int]$MaxCost = 30,
  [int]$MaxStates = 300000
)

function Fail($msg){
  [PSCustomObject]@{ ok=$false; error=$msg } | ConvertTo-Json -Depth 6
  exit 1
}

$pyScript = Join-Path $PSScriptRoot 'solve_esn.py'
if(-not (Test-Path $pyScript)){ Fail "solver script not found: $pyScript" }

# Locate a real Python 3 (skip the WindowsApps store stub, which is not an interpreter).
$python = $null
$candidates = @()
$installed = Get-ChildItem "$env:LOCALAPPDATA\Programs\Python\Python3*\python.exe" -ErrorAction SilentlyContinue |
             Sort-Object FullName -Descending
if($installed){ $candidates += @($installed | ForEach-Object { $_.FullName }) }
foreach($name in @('py','python3','python')){
  $cmd = Get-Command $name -ErrorAction SilentlyContinue
  if($cmd -and $cmd.Source -notmatch 'WindowsApps'){ $candidates += $cmd.Source }
}
foreach($c in $candidates){
  $v = & $c --version 2>$null
  if($LASTEXITCODE -eq 0 -and "$v" -match 'Python 3'){ $python = $c; break }
}
if(-not $python){ Fail "no Python 3 interpreter found (looked in $env:LOCALAPPDATA\Programs\Python and PATH); install Python 3 to run the solver" }

& $python $pyScript --esn-path $EsnPath --max-cost $MaxCost --max-states $MaxStates
exit $LASTEXITCODE
