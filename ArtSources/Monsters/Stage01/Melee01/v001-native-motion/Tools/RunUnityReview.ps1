param([switch]$PrepareOnly,[switch]$ResumeSaved)
$ErrorActionPreference='Stop'
$sourceRoot=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qaProject=Join-Path $sourceRoot 'QA/UnityProject'
$delivery=Join-Path $qaProject 'Assets/Art/Monsters/Stage01/Melee01/v001'
$inputFbx=Join-Path $sourceRoot 'Exports/Stage01_Melee01.fbx'
if(-not(Test-Path -LiteralPath $inputFbx)){throw "Waiting for source export: $inputFbx"}
if($ResumeSaved){$stagedFbx=Join-Path $delivery 'FBX/Stage01_Melee01.fbx'; if(-not(Test-Path -LiteralPath $stagedFbx)){throw 'No saved import to resume'}; if((Get-FileHash -LiteralPath $inputFbx).Hash -ne (Get-FileHash -LiteralPath $stagedFbx).Hash){throw 'Source FBX changed; saved-asset review cannot resume'}}
New-Item -ItemType Directory -Force -Path (Join-Path $delivery 'FBX'),(Join-Path $delivery 'Textures'),(Join-Path $delivery 'Shaders'),(Join-Path $qaProject 'Assets/Editor') | Out-Null
Copy-Item -LiteralPath $inputFbx -Destination (Join-Path $delivery 'FBX/Stage01_Melee01.fbx')
$textures=Join-Path $sourceRoot 'Exports/Textures'
if(Test-Path -LiteralPath $textures){Get-ChildItem -LiteralPath $textures -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $delivery 'Textures')}}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MonsterAtlas.shader') -Destination (Join-Path $delivery 'Shaders/MonsterAtlas.shader')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MonsterUnityReview.cs') -Destination (Join-Path $qaProject 'Assets/Editor/MonsterUnityReview.cs')
if($PrepareOnly){Write-Output "STAGED_NATIVE_MONSTER $delivery";return}
$unityExe='C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe'
$unityLog=Join-Path $sourceRoot 'QA/unity-render.log'
# Graphics are intentionally enabled: this produces real Unity renders.
$entrypoint=if($ResumeSaved){'MonsterUnityReview.RenderSaved'}else{'MonsterUnityReview.BuildAndRender'}
$taskArgs=@('-batchmode','-quit','-projectPath',"`"$qaProject`"",'-executeMethod',$entrypoint,'-logFile',"`"$unityLog`"")
$process=Start-Process -FilePath $unityExe -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
Write-Output "UNITY_PID=$($process.Id) LOG=$unityLog"
