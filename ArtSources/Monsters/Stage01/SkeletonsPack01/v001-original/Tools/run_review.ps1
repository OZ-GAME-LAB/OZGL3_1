param([ValidateSet('BuildAndPreview','BuildAndRender','PreviewSaved','RenderSaved','AttachmentStudy','PoseDiagnostics','ResampleDiagnostics','MageLeftStudy','EquipmentFitStudy')][string]$Mode='BuildAndPreview')
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot '../../../../../'))
$qaProject=Join-Path $projectRoot '.local-validation/SkeletonsPack01'
$env:SKELETON_REVIEW_ROOT=$sourceRoot
Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $qaProject 'Assets/Editor')}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SkeletonNativeAtlas.shader') -Destination (Join-Path $qaProject 'Assets/Art/Monsters/Stage01/SkeletonsPack01/v001/Shaders/SkeletonNativeAtlas.shader')
$unityExe='C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe'
$log=Join-Path $sourceRoot ('QA/unity-'+$Mode+'.log')
$args=@('-batchmode','-quit','-projectPath',('"'+$qaProject+'"'),'-executeMethod',('SkeletonPackReview.'+$Mode),'-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath $unityExe -ArgumentList $args -WindowStyle Hidden -PassThru
Write-Output "PID=$($process.Id) MODE=$Mode LOG=$log"
