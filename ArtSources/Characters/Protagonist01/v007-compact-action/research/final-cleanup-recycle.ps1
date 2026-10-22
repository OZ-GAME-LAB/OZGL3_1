[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$KeepSession,
    [Parameter(Mandatory = $true)][string]$FinalReport,
    [string[]]$KeepRootFile = @(),
    [switch]$Execute
)

# Default is an inventory-only dry run. Root calls this only after final
# capture, export, and review; no running capture files are touched.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName Microsoft.VisualBasic
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../../'))
$workspacePrefix = $workspace.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$v007 = [IO.Path]::GetFullPath((Join-Path $workspace 'ArtSources/Characters/Protagonist01/v007-compact-action'))
$captureRoot = Join-Path $v007 'CaptureRuns'
$outputRoot = Join-Path $v007 'ReviewOutput'
$finalOutput = Join-Path $outputRoot 'FinalChibi'
$inventoryPath = Join-Path $PSScriptRoot 'final-cleanup-recycle-inventory.json'

function Assert-WorkspacePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target escapes the verified workspace: $full"
    }
    return $full
}

if (-not (Test-Path -LiteralPath (Join-Path $workspace 'Assets') -PathType Container)) {
    throw "Workspace root does not contain Assets: $workspace"
}
if ($KeepSession -notmatch '^\d{8}_\d{9}$') {
    throw 'KeepSession must be one exact UTC capture-directory name (yyyyMMdd_HHmmssfff).'
}
$keepCapture = Assert-WorkspacePath (Join-Path $captureRoot $KeepSession)
if (-not (Test-Path -LiteralPath $keepCapture -PathType Container)) {
    throw "Final capture session does not exist: $keepCapture"
}
$reportPath = Assert-WorkspacePath ([IO.Path]::GetFullPath($FinalReport))
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (@($report.motions).Count -ne 18 -or @($report.transitions).Count -ne 2) {
    throw 'Final report must contain the complete current 18 motions and 2 transitions.'
}
$capturePrefix = $keepCapture.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
foreach ($item in @($report.motions) + @($report.transitions) + @($report.details)) {
    if ($null -eq $item) { continue }
    $framesPath = Assert-WorkspacePath ([string]$item.framesDirectory)
    if (-not $framesPath.StartsWith($capturePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Report still references a capture outside the preserved final session: $framesPath"
    }
    $frameFiles = @(Get-ChildItem -LiteralPath $framesPath -File -Filter '*.png')
    if ($frameFiles.Count -ne [int]$item.frameCount) {
        throw "Incomplete final capture $($item.name): $($frameFiles.Count) PNGs, expected $($item.frameCount)."
    }
}
foreach ($required in @('index.html', 'delivery-validation.json', 'export-manifest.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $finalOutput $required) -PathType Leaf)) {
        throw "Complete FinalChibi export required before cleanup: $required"
    }
}

# Exact known obsolete directories. The FinalChibi directory and canonical
# index.html are intentionally not targets; root writes the latest redirect.
$relativeTargets = @(
    'ArtSources/Characters/Protagonist01/v007-compact-action/Frames',
    'ArtSources/Characters/Protagonist01/v007-compact-action/GripDetails',
    'ArtSources/Characters/Protagonist01/v007-compact-action/Transitions',
    'ArtSources/Characters/Protagonist01/v007-compact-action/QAHistory',
    'ArtSources/Characters/Protagonist01/v007-compact-action/__pycache__',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/Auxiliary',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/Contacts',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/GIF',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/Groups',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/IndependentCombatQA',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/IndependentHandRegressionQA',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/IndependentQA',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/QA',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/Transitions',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/delivery-validation.json',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/export-manifest.json',
    'ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/README_ko.txt',
    'ArtSources/Characters/Protagonist01/v006-chibi-cap/ElasticReview',
    'ArtSources/Characters/Protagonist01/v006-chibi-cap/ReviewOutput',
    'ArtSources/Stage01Kit/research/StaffGripProbe',
    'ArtSources/Stage01Kit/research/StaffGripProbe_Calibrated',
    'ArtSources/Stage01Kit/research/GripFinalVisualQA',
    'ArtSources/Stage01Kit/research/__pycache__'
)
$oldRootFiles = @(
    'closed-volume-current.png', 'final-chibi-grip-contact.png',
    'rebuild-final-fit-main.png', 'rebuild-first-poses.png', 'rebuild-main-poses.png',
    'reference-center-fit-contact.png', 'reference-placement45-contact.png',
    'reference-thumb-web-contact.png',
    'final-chibi-closed-volume-unity.log', 'final-chibi-migration-grip-unity.log',
    'final-chibi-native-sampling-unity.log', 'final-chibi-remodelled-grip-unity.log',
    'final-chibi-grip-verification-unity.log', 'final-chibi-fixed-grip-unity.log',
    'final-chibi-complete-unity.log',
    'final-chibi-complete-probe-unity.log',
    'final-chibi-wrist-collar-unity.log',
    'reference-final-camera-contact-unity.log', 'reference-final-delivery-unity.log',
    'reference-grip-center-fit-unity.log', 'reference-grip-diagonal-unity.log',
    'reference-grip-final-fit-unity.log', 'reference-grip-placement45-unity.log',
    'reference-local-topology-final-unity.log', 'reference-rebuild-final-unity.log',
    'reference-rebuild-unity.log', 'reference-thumb-web-unity.log',
    'reference-wrist-corrective-unity.log'
)
foreach ($name in $oldRootFiles) {
    if ($KeepRootFile -notcontains $name) {
        $relativeTargets += "ArtSources/Characters/Protagonist01/v007-compact-action/$name"
    }
}
# Retire every old direct output child while explicitly preserving the final
# package and canonical redirect, including any old export subfolder not in
# the earlier inventory. Parent and final absolute paths are checked below.
foreach ($child in Get-ChildItem -LiteralPath $outputRoot) {
    if ($child.Name -eq 'FinalChibi' -or $child.Name -eq 'index.html') { continue }
    $relativeTargets += [IO.Path]::GetRelativePath($workspace, $child.FullName)
}
# Capture directories are direct children with a strictly validated timestamp.
# Unknown names cause a stop rather than broadening the deletion selection.
foreach ($dir in Get-ChildItem -LiteralPath $captureRoot -Directory) {
    if ($dir.Name -eq $KeepSession) { continue }
    if ($dir.Name -notmatch '^\d{8}_\d{9}$') {
        throw "Unrecognized capture-directory name; root must review it: $($dir.FullName)"
    }
    $relativeTargets += [IO.Path]::GetRelativePath($workspace, $dir.FullName)
}

$targets = @()
$seenTargets = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($relative in ($relativeTargets | Select-Object -Unique)) {
    $absolute = Assert-WorkspacePath (Join-Path $workspace $relative)
    if (-not $seenTargets.Add($absolute)) { continue }
    if (-not (Test-Path -LiteralPath $absolute)) { continue }
    if ($absolute -eq $keepCapture -or $absolute.StartsWith($capturePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to retire the preserved final capture: $absolute"
    }
    if ($absolute -eq $finalOutput -or $absolute.StartsWith($finalOutput + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to retire final output: $absolute"
    }
    $item = Get-Item -LiteralPath $absolute
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing a junction/symlink cleanup target: $absolute"
    }
    $files = if ($item.PSIsContainer) { @(Get-ChildItem -LiteralPath $absolute -File -Recurse) } else { @($item) }
    $fileRows = @()
    foreach ($file in $files) {
        $fileRows += [ordered]@{
            path = [IO.Path]::GetRelativePath($workspace, $file.FullName).Replace('\', '/')
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $targetByteCount = [long]0
    foreach ($row in $fileRows) { $targetByteCount += [long]$row.bytes }
    $targets += [ordered]@{
        path = $absolute
        relativePath = $relative.Replace('\', '/')
        kind = if ($item.PSIsContainer) { 'directory' } else { 'file' }
        fileCount = $fileRows.Count
        bytes = $targetByteCount
        files = $fileRows
        status = 'inventoried-not-removed'
    }
}
$inventory = [ordered]@{
    recordedUTC = [DateTime]::UtcNow.ToString('o')
    mode = if ($Execute) { 'recycle-bin' } else { 'dry-run' }
    scope = 'Only explicit obsolete previews/attempts and earlier timestamped capture sessions. All resolved targets were verified beneath the workspace. Source chibi assets, final capture, final export, research evidence, current reports, and canonical index are preserved.'
    workspace = $workspace
    preservedFinalCapture = $keepCapture
    finalReport = $reportPath
    finalReportSHA256 = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash.ToLowerInvariant()
    targets = $targets
}
function Save-Inventory {
    $inventory | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $inventoryPath -Encoding utf8
}
Save-Inventory
if ($Execute) {
    foreach ($target in $targets) {
        # Verify the exact already-inventoried absolute path again immediately
        # before the recursive recycling operation; no other shell is used.
        $absolute = Assert-WorkspacePath $target.path
        try {
            if ($target.kind -eq 'directory') {
                [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($absolute,
                    [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
                    [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
            } else {
                [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($absolute,
                    [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
                    [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
            }
            $target.status = 'removed-to-recycle-bin'
        } catch {
            $target.status = 'failed-retained'
            $target['error'] = $_.Exception.Message
        }
        Save-Inventory
    }
}
$totalFileCount = [long]0
$totalBytes = [long]0
foreach ($target in $targets) {
    $totalFileCount += [long]$target.fileCount
    $totalBytes += [long]$target.bytes
}
[ordered]@{
    mode = $inventory.mode
    targetCount = $targets.Count
    fileCount = $totalFileCount
    bytes = $totalBytes
    failedTargets = @($targets | Where-Object { $_.status -eq 'failed-retained' }).Count
    inventory = $inventoryPath
} | ConvertTo-Json -Compress
