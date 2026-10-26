$ErrorActionPreference = 'Stop'
$planFile = Join-Path $PSScriptRoot 'cleanup-plan.json'
$plan = Get-Content -LiteralPath $planFile -Raw | ConvertFrom-Json
if (-not $plan.gatePassed -or $plan.selectedClips -ne 18) { throw 'Preservation gate is not complete.' }
$sourceRoot = [IO.Path]::GetFullPath([string]$plan.sourceRoot).TrimEnd('\')
$assetRoot = [IO.Path]::GetFullPath([string]$plan.assetRoot).TrimEnd('\')
$allowedSourceRoot = 'C:\Users\me\Desktop\OZGameLab3\OZGL3_1\ArtSources\Characters\Protagonist01'
$allowedAssetRoot = 'C:\Users\me\Desktop\OZGameLab3\OZGL3_1\Assets\Art\Characters\Protagonist01'
if ($sourceRoot -ne $allowedSourceRoot -or $assetRoot -ne $allowedAssetRoot) { throw 'Unexpected workspace roots.' }
$targets = @()
foreach ($row in $plan.deleteTargets) {
    $item = Get-Item -LiteralPath ([string]$row.path) -Force
    $resolved = [IO.Path]::GetFullPath($item.FullName)
    $isInside = $resolved.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or $resolved.StartsWith($assetRoot + '\', [StringComparison]::OrdinalIgnoreCase)
    if (-not $isInside -or $resolved.StartsWith($sourceRoot + '\Final', [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe deletion target: $resolved" }
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Linked deletion root rejected: $resolved" }
    $targets += $resolved
}
# All resolved absolute targets have now been validated before any removal.
Write-Output ('Validated absolute targets: ' + $targets.Count)
$deleted = @()
foreach ($target in $targets) {
    Write-Output ('Removing approved unused files: ' + $target)
    Remove-Item -LiteralPath $target -Recurse -Force
    if (Test-Path -LiteralPath $target) { throw "Target still exists: $target" }
    $deleted += $target
    @{ completed = $deleted.Count; total = $targets.Count; deleted = $deleted } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'cleanup-progress.json') -Encoding utf8
}
@{ completed = $true; deleted = $deleted; deletedFileCount = $plan.deleteFileCount; deletedBytes = $plan.deleteBytes } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'cleanup-result.json') -Encoding utf8
Write-Output 'CLEANUP_DELETION_COMPLETE'
