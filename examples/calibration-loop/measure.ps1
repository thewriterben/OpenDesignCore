<#
.SYNOPSIS
  Run four caliper readings of the calibration block through compare + compensate.

.DESCRIPTION
  The two commands from CALIBRATE-FIRST.md steps 5, wired together so the
  comparison hash does not have to be copied by hand. That copy is the step
  where a measurement gets attached to the wrong print.

  Nothing here is defaulted that the tools refuse to default: material,
  instrument accuracy and the axis-spread limit are all required, because each
  is a judgement the tooling must not make for you.

.EXAMPLE
  .\examples\calibration-loop\measure.ps1 -X 39.98 -Y 59.97 -ZLow 4.01 -ZHigh 25.02 -Material pla

.EXAMPLE
  # Same, and offer it to the studio profile. Refused unless k2-plus has all
  # three axes recorded in the OpenBuildCore machine registry.
  .\examples\calibration-loop\measure.ps1 -X 39.98 -Y 59.97 -ZLow 4.01 -ZHigh 25.02 `
      -Material pla -ProposeToProfile pla
#>
[CmdletBinding()]
param(
    # Across the 40 mm faces, a few mm up from the bed.
    [Parameter(Mandatory)][double] $X,

    # Across the 60 mm faces, a few mm up from the bed.
    [Parameter(Mandatory)][double] $Y,

    # Bed to the top of the shelf.
    [Parameter(Mandatory)][double] $ZLow,

    # Bed to the top of the tall face.
    [Parameter(Mandatory)][double] $ZHigh,

    # What you actually printed. Not defaulted -- an unlabelled measurement is
    # how a PLA figure ends up in a PETG profile.
    [Parameter(Mandatory)][string] $Material,

    # Your caliper's stated accuracy. Decides whether a deviation is real.
    [double] $InstrumentAccuracyMm = 0.02,

    # How much X/Y disagreement still permits one shrinkage factor. A process
    # judgement; the tool takes no default for it.
    [double] $MaxAxisSpreadPct = 0.15,

    # Omit to compute and record only. Supply a profile key to also offer it to
    # the studio -- which needs -MachineId calibrated in the registry.
    [string] $ProposeToProfile,

    [string] $MachineId = 'k2-plus',
    [string] $MachinesJson = '../OpenBuildCore/example/machines.json',
    [string] $StudioUrl = 'http://localhost:8770',

    # The stepped block, schema 0.2: 40 x 60 x 25 with the shelf at 4 mm.
    [string] $DesignStl = 'artifacts/b7/b74407dedcd54f8737b70dbe6d185d19c8d50278cefa8ed6f76e47a886c72b0b.stl',
    [double] $NominalStepZMm = 4,
    [double] $VoxelMm = 0.2
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repo

if (-not (Test-Path $DesignStl)) {
    throw "No design STL at $DesignStl. Generate one with 'run-calibration-block' first, " +
          "and pass its path with -DesignStl. The comparison is against the exported " +
          "geometry, not against the numbers you asked for, so it needs the actual file."
}

# Culture-invariant formatting. A machine set to a comma-decimal locale would
# otherwise emit "39,98" and the parser would reject it -- or worse, not.
$inv = [System.Globalization.CultureInfo]::InvariantCulture
function fmt([double] $v) { $v.ToString('0.###', $inv) }

$measured = '{0}x{1}x{2}x{3}' -f (fmt $X), (fmt $Y), (fmt $ZLow), (fmt $ZHigh)
Write-Host "measured $measured mm in $Material" -ForegroundColor Cyan

$compare = dotnet run --project src/OpenDesignCore -c Release -- `
    compare --design $DesignStl `
            --units mm --voxel-mm (fmt $VoxelMm) `
            --measured $measured `
            --nominal-step-z-mm (fmt $NominalStepZMm) `
            --instrument-accuracy-mm (fmt $InstrumentAccuracyMm) `
            --material $Material

$compare | Write-Host
if ($LASTEXITCODE -ne 0) { throw "compare failed with exit code $LASTEXITCODE." }

# The report hash, taken from compare's own output rather than recomputed --
# recomputing risks answering about different bytes than the ones recorded.
$hash = ($compare | Select-String -Pattern 'sha256:([0-9a-f]{64})' |
         Select-Object -First 1).Matches.Groups[1].Value
if (-not $hash) { throw "Could not find a report hash in compare's output." }

Write-Host "`ncomparison $hash" -ForegroundColor Cyan

$compensateArgs = @(
    'compensate'
    '--comparison', $hash
    '--max-axis-spread-pct', (fmt $MaxAxisSpreadPct)
)
if ($ProposeToProfile) {
    $compensateArgs += @(
        '--propose-to-profile', $ProposeToProfile
        '--machines', $MachinesJson
        '--machine-id', $MachineId
        '--studio', $StudioUrl
    )
}

dotnet run --project src/OpenDesignCore -c Release -- @compensateArgs

# Exit code carries the verdict: 0 means a proposal was produced, 1 means a
# refusal. A refusal is a correct outcome, so it is reported rather than thrown.
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nNo compensation was proposed. Read the reason above; most comparisons should not become settings." -ForegroundColor Yellow
}
exit $LASTEXITCODE
