# End-to-end walkthrough across all four platform peers.
#
# Deliberately shells out to each peer's own CLI: this demonstrates
# composition, not a code dependency. If a step breaks, it breaks the way a
# user would experience it.
#
#   .\run.ps1 [-Root F:\Documents\GitHub]
param(
    [string]$Root = "F:\Documents\GitHub",
    [string]$KiCadBin = "$env:LOCALAPPDATA\Programs\KiCad\10.0\bin",
    [string]$StageDir = "F:\Documents\3DP\slicing-inbox"
)

$ErrorActionPreference = "Stop"
$build   = Join-Path $Root "OpenBuildCore"
$circuit = Join-Path $Root "OpenCircuitCore"
$design  = Join-Path $Root "OpenDesignCore"
$board   = Join-Path $circuit "boards\reference-esp32s3"
$cli     = Join-Path $KiCadBin "kicad-cli.exe"
$kpython = Join-Path $KiCadBin "python.exe"

function Step($n, $text) { Write-Host "`n=== $n. $text ===" -ForegroundColor Cyan }

# 1. What can I build from what I own?  (OpenBuildCore -> OpenPartsCore)
Step 1 "OpenBuildCore: is env-monitor buildable, and what should I buy?"
Push-Location $build
python scripts\advisor.py gaps env-monitor
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "env-monitor is not buildable from the example inventory" }
python scripts\advisor.py shopping-list
Pop-Location

# 2. The electronics for it.  (OpenCircuitCore -> OpenPartsCore)
Step 2 "OpenCircuitCore: schematic, ERC, BOM"
Push-Location $circuit
python scripts\make_reference_schematic.py
Pop-Location
Push-Location $board
& $cli sch erc --exit-code-violations --severity-error -o erc-report.txt sensor-subcircuit.kicad_sch
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "ERC found violations" }
& $cli sch export bom --fields "Reference,Value,Footprint,opc_id" --group-by Value -o sensor-bom.csv sensor-subcircuit.kicad_sch
Get-Content sensor-bom.csv
Pop-Location

# 3. The board, checked and exported as geometry.
Step 3 "OpenCircuitCore: board DRC and STL export"
Push-Location $circuit
& $kpython scripts\make_reference_board.py
Pop-Location
Push-Location $board
& $cli pcb drc --exit-code-violations --severity-error -o drc-report.txt reference-esp32s3.kicad_pcb
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "DRC found violations" }
& $cli pcb export stl -o reference-esp32s3.stl reference-esp32s3.kicad_pcb
Pop-Location

# 4. An enclosure fitted to that actual board.  (OpenDesignCore)
Step 4 "OpenDesignCore: enclosure fitted to the real board"
Push-Location $design
$stl = Join-Path $board "reference-esp32s3.stl"
$runOutput = dotnet run --project src/OpenDesignCore -c Release --no-build -- `
    run-cradle --stl $stl --units mm --voxel-mm 0.3 `
    --clearance-mm 0.4 --wall-mm 2.4 --split 0.9 2>&1 | Out-String
Write-Host $runOutput
$runId = ([regex]::Match($runOutput, 'run (\d+): PASS')).Groups[1].Value
if (-not $runId) { Pop-Location; throw "cradle run failed or run id unreadable" }

# 5. Staged for fabrication, provenance intact.
Step 5 "OpenDesignCore: stage for fabrication"
dotnet run --project src/OpenDesignCore -c Release --no-build -- `
    handoff --run $runId --stage $StageDir --offline
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "handoff failed" }
Pop-Location

Write-Host "`nWalkthrough complete: inventory -> electronics -> board -> enclosure -> staged." -ForegroundColor Green
Write-Host "Every artifact is content-addressed and its run is in ledger.db."
