# OZGL3_1
오즈게임랩 3기 1팀

## Getting started

1. Clone this repository.
2. Install Unity **6000.3.21f1** through Unity Hub.
3. In Unity Hub, choose **Add project from disk** and select this repository's
   `OZGL3_1` folder (the folder containing `Assets`, `Packages`, and `ProjectSettings`).
4. Open the project and let Unity restore packages and import assets.
5. Open `Assets/Scenes/MainScene.unity`.

## Repository layout

```text
OZGL3_1/                       Unity project root
├── .gitattributes             Shared text-file settings
├── .gitignore                 Generated and local-file exclusions
├── Assets/                    Scenes, scripts, assets, and their .meta files
├── Packages/                  Package manifest and dependency lockfile
├── ProjectSettings/           Shared Unity project settings
└── README.md
```

Commit asset changes together with their `.meta` files. Commit both package JSON
files when changing dependencies. Text serialization and visible metadata are
enabled for version control.

Unity recreates `Library`, `Temp`, `Logs`, and other generated files locally.
These folders, personal settings, and generated IDE project files are ignored.

For a character test, use `Assets/2.Prefab/Player/Player_Chibi.prefab`. The
staff animation example is `Player_Chibi_StaffTipBolt.prefab` under
`Assets/Art/Characters/Protagonist01/AnimationStudies/StaffTipBolt20261102/`.
In Play Mode it loops the armed idle. Select the instance and trigger
`Cast` in the Animator window to play the shot and return to idle.

Keep capture frames, GIFs, personal reports and prompts outside the Unity
project. `ArtSources/` holds editable art and its source licenses.
