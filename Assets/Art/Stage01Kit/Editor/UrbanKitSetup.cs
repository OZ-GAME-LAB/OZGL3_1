using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class UrbanKitSetup
{
    public const string Root="Assets/Art/Stage01Kit";
    public const string ScenePath=Root+"/UrbanKit_Preview.unity";
    private const string Hero="Assets/Art/Characters/Protagonist01/Runtime";
    private const string Source="ArtSources/Stage01Kit";
    [Serializable]private class Entry{public string name;public int triangles;public int renderers;public int colliders;public bool vertexColors;}
    [Serializable]private class Report
    {
        public bool success;public string unity;public int modularPrefabs;public int props;public int weapons;
        public int sharedEnvironmentMaterials;public bool shaderSupported;public bool shaderErrors;
        public bool swordSocketBound;public bool staffSocketBound;public bool attackOverridesBound;public bool unarmedRestore;
        public bool platformRoofsValidated;public string[] blocks;public string scene;public List<Entry> assets=new List<Entry>();
    }

    [MenuItem("Tools/Project M/Build Tutorial and Stage 1 Art Kit")]
    public static void Build()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        try
        {
            Directory.CreateDirectory(Source);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UrbanModuleBuilder.Build();
            UrbanPropBuilder.Build();
            ProtagonistExtendedAnimationSetup.Build();
            SideviewAnimationRefinement.Build();
            ProtagonistGripBuilder.Build();
            ProtagonistGroundingBuilder.Build();
            UrbanWeaponBuilder.Build();
            UrbanConsumableBuilder.Calibrate();
            ProtagonistExtendedValidation.Validate();
            BuildBlocks();
            BuildEquippedVariants();
            BuildPreviewScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Capture();
            Debug.Log("URBAN_KIT_BUILD_SUCCESS "+ScenePath);
        }
        catch(Exception exception){Debug.LogException(exception);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }

    public static void BuildWithReview()
    {
        Build();
        UrbanCharacterCapture.Capture();
    }

    public static void BuildSideviewReview()
    {
        Build();
        SideviewContactCalibration.Analyze();
        SideviewReviewCapture.CaptureAll();
    }

    private static GameObject Place(GameObject parent,string group,string name,Vector3 position,float rotation=0)
    {
        string path=Root+"/Prefabs/"+group+"/"+name+".prefab";
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null)throw new InvalidOperationException("Missing kit prefab: "+path);
        GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.transform);
        instance.transform.localPosition=position;instance.transform.localRotation=Quaternion.Euler(0,rotation,0);
        return instance;
    }
    private static void Marker(GameObject root,string name,Vector3 point){new GameObject(name).transform.SetParent(root.transform,false);root.transform.Find(name).localPosition=point;}

    private static void BuildBlocks()
    {
        Directory.CreateDirectory(Root+"/Prefabs/Blocks");AssetDatabase.Refresh();
        foreach(bool tutorial in new[]{true,false})
        {
            var block=new GameObject(tutorial?"Tutorial_Block":"Stage01_Block");
            try
            {
                int roads=tutorial?4:8;
                for(int i=0;i<roads;i++)Place(block,"Modules","Road_Straight_12m",new Vector3(i*12+6,0,0));
                for(int i=0;i<(tutorial?6:12);i++)Place(block,"Modules","Sidewalk_Straight_8m",new Vector3(i*8+4,.02f,-4));
                if(tutorial)
                {
                    Place(block,"Modules","Building_ShopLow",new Vector3(18,0,0));
                    Place(block,"Modules","Exterior_Stairs_Rise3m",new Vector3(11,0,0));
                    Place(block,"Modules","Rooftop_Bridge_6m",new Vector3(26,4,0));
                    Place(block,"Modules","Building_ShopLow",new Vector3(33,0,0));
                    Place(block,"Props","GatePylon",new Vector3(35,4,1.7f));
                    Place(block,"Modules","Ladder_4m",new Vector3(20,0,-2.15f));
                    Place(block,"Modules","Utility_Box",new Vector3(31,4,1.1f));
                    Marker(block,"PlayerSpawn",new Vector3(2,.04f,0));Marker(block,"TutorialMonsterAnchor",new Vector3(8,.05f,0));
                    Marker(block,"TutorialGateAnchor",new Vector3(35,4,0));Marker(block,"Stage01Exit",new Vector3(45,0,0));
                }
                else
                {
                    Place(block,"Modules","Building_ShopLow",new Vector3(9,0,0));
                    Place(block,"Modules","Rooftop_Bridge_6m",new Vector3(16,4,0));
                    Place(block,"Modules","Building_OfficeMid",new Vector3(24,0,0));
                    Place(block,"Modules","Rooftop_Bridge_6m",new Vector3(31,8,0));
                    Place(block,"Modules","Building_ApartmentTall",new Vector3(40,0,0));
                    Place(block,"Modules","Rooftop_Bridge_6m",new Vector3(48,12,0));
                    Place(block,"Modules","Building_OfficeMid",new Vector3(56,0,0));
                    Place(block,"Modules","Building_RooftopArena",new Vector3(73,0,0));
                    Place(block,"Modules","Rooftop_Hut",new Vector3(38,12,1.6f));
                    Place(block,"Modules","Rooftop_WaterTank",new Vector3(42,12,1.5f));
                    Place(block,"Modules","Rooftop_Billboard",new Vector3(23,8,1.8f));
                    Place(block,"Props","ACUnit",new Vector3(54,8,1.6f));
                    Place(block,"Props","ACUnit",new Vector3(58,8,1.6f));
                    for(int i=0;i<3;i++)
                    {
                        float x=new[]{24,40,56}[i],y=new[]{8,12,8}[i];
                        Place(block,"Props","GatePylon",new Vector3(x,y,1.65f));Marker(block,"NormalGate_"+(i+1),new Vector3(x,y,0));
                    }
                    Place(block,"Props","GatePylon",new Vector3(79,4,1.8f));
                    Marker(block,"BossGateAnchor",new Vector3(79,4,0));Marker(block,"BossArenaAnchor",new Vector3(72,4,0));
                    Marker(block,"PlayerSpawn",new Vector3(2,.04f,0));
                }
                float length=tutorial?48:96;
                for(float x=3;x<length;x+=12)Place(block,"Props","StreetLamp",new Vector3(x,.04f,-3.8f));
                for(float x=6;x<length;x+=16)Place(block,"Props","StreetTree",new Vector3(x,.04f,-4.3f));
                Place(block,"Props","VendingMachine",new Vector3(9,.04f,-3.7f));
                Place(block,"Props","Bench",new Vector3(5,.04f,-3.6f));
                Place(block,"Props","TrashBin",new Vector3(10.5f,.04f,-3.7f));
                Place(block,"Modules","Planter_Shrubs",new Vector3(15,.04f,-4));
                Place(block,"Modules","Road_Barrier",new Vector3(1,.04f,-2.7f));
                foreach(float x in tutorial?new[]{15f,21f,30f,36f}:new[]{21f,27f,37f,43f,53f,59f})
                    Place(block,"Modules","Guardrail_4m",new Vector3(x,tutorial?4:x<30?8:x<50?12:8,2));
                PrefabUtility.SaveAsPrefabAsset(block,Root+"/Prefabs/Blocks/"+block.name+".prefab");
            }
            finally{Object.DestroyImmediate(block);}
        }
    }

    private static void BuildEquippedVariants()
    {
        foreach(string name in new[]{"Sword","Staff"})
        {
            GameObject actor=PrefabUtility.LoadPrefabContents(Hero+"/Protagonist01_Player.prefab");
            try
            {
                actor.name="Protagonist01_"+name+"_Equipped";
                var weapon=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Weapons/"+name+".prefab").GetComponent<SocketWeapon>();
                if(!actor.GetComponent<HandSocketEquipment>().Equip(weapon))throw new InvalidOperationException("Cannot attach "+name);
                PrefabUtility.SaveAsPrefabAsset(actor,Root+"/Prefabs/Weapons/"+actor.name+".prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(actor);}
        }
    }

    private static void BuildPreviewScene()
    {
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var tutorial=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Blocks/Tutorial_Block.prefab"));
        var stage=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Blocks/Stage01_Block.prefab"));
        stage.transform.position=new Vector3(48,0,0);
        var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Hero+"/Protagonist01_Player.prefab"));
        actor.transform.position=new Vector3(2,.05f,0);actor.transform.rotation=Quaternion.LookRotation(Vector3.right);
        var controls=actor.AddComponent<UrbanKitPreviewControls>();
        controls.Configure(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Weapons/Sword.prefab").GetComponent<SocketWeapon>(),
            AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Weapons/Staff.prefab").GetComponent<SocketWeapon>(),
            AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Props/Potion_Health.prefab"));
        var camera=new GameObject("Main Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";
        camera.orthographic=true;camera.orthographicSize=5.8f;camera.transform.position=new Vector3(3.3f,2.65f,-11);
        camera.transform.LookAt(new Vector3(2,1.3f,0));camera.backgroundColor=new Color(.11f,.16f,.22f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.gameObject.AddComponent<ProtagonistPlaygroundCamera>().Configure(actor.transform);
        var light=new GameObject("Sun",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=.85f;light.color=new Color(.84f,.89f,.96f);
        light.transform.rotation=Quaternion.Euler(42,-35,0);light.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.36f,.41f,.47f);RenderSettings.fog=false;
        var ambientProbe=new SphericalHarmonicsL2();ambientProbe.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=ambientProbe;
        EditorSceneManager.SaveScene(scene,ScenePath);
    }

    public static void Validate()
    {
        var report=new Report{unity=Application.unityVersion,scene=ScenePath,blocks=new[]{"Tutorial_Block","Stage01_Block"}};
        foreach(string group in new[]{"Modules","Props","Weapons"})
        {
            foreach(string path in Directory.GetFiles(Root+"/Prefabs/"+group,"*.prefab"))
            {
                if(Path.GetFileName(path).StartsWith("Protagonist01_"))continue;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filters=prefab.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length==0)throw new InvalidOperationException("Empty art prefab "+path);
                var entry=new Entry{name=Path.GetFileNameWithoutExtension(path),triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3),renderers=prefab.GetComponentsInChildren<MeshRenderer>(true).Length,
                    colliders=prefab.GetComponentsInChildren<Collider>(true).Length,vertexColors=filters.All(f=>f.sharedMesh.colors.Length==f.sharedMesh.vertexCount)};
                if(!entry.vertexColors)throw new InvalidOperationException("Missing vertex palette "+path);
                if(prefab.GetComponentsInChildren<MeshCollider>(true).Length>0)throw new InvalidOperationException("Unexpected mesh collision "+path);
                report.assets.Add(entry);
                if(group=="Modules")report.modularPrefabs++;else if(group=="Props")report.props++;else report.weapons++;
            }
        }
        report.sharedEnvironmentMaterials=report.assets.SelectMany(e=>FindPrefab(e.name).GetComponentsInChildren<MeshRenderer>(true)).SelectMany(r=>r.sharedMaterials).Distinct().Count();
        Shader shader=Shader.Find("ProjectM/Urban Vertex Color");report.shaderSupported=shader!=null&&shader.isSupported;report.shaderErrors=shader!=null&&ShaderUtil.ShaderHasError(shader);
        if(!report.shaderSupported||report.shaderErrors)throw new InvalidOperationException("Kit shader unavailable or has compile errors");
        GameObject actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Hero+"/Protagonist01_Player.prefab"));
        try
        {
            var equipment=actor.GetComponent<HandSocketEquipment>();var animator=actor.GetComponent<Animator>();var original=animator.runtimeAnimatorController;
            foreach(string name in new[]{"Sword","Staff"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Weapons/"+name+".prefab").GetComponent<SocketWeapon>();
                if(!equipment.Equip(prefab))throw new InvalidOperationException("Cannot equip "+name);
                bool bound=equipment.Equipped.transform.parent.name=="WeaponSocket"&&equipment.Equipped.transform.IsChildOf(actor.transform);
                if(name=="Sword")report.swordSocketBound=bound;else report.staffSocketBound=bound;
                report.attackOverridesBound=animator.runtimeAnimatorController==prefab.AnimationOverride;
                if(!bound||!report.attackOverridesBound)throw new InvalidOperationException("Weapon binding failed "+name);
            }
            equipment.Unequip();report.unarmedRestore=animator.runtimeAnimatorController==original;
            if(!report.unarmedRestore)throw new InvalidOperationException("Cannot restore unarmed controller");
        }
        finally{Object.DestroyImmediate(actor);}
        foreach(string name in new[]{"Building_ShopLow","Building_OfficeMid","Building_ApartmentTall","Building_RooftopArena"})
        {
            GameObject building=Object.Instantiate(FindPrefab(name));building.transform.position=new Vector3(1000,0,1000);
            try
            {
                Transform roof=building.transform.Find("RoofSurface");Physics.SyncTransforms();
                if(roof==null||!Physics.Raycast(roof.position+Vector3.up,Vector3.down,out RaycastHit hit,2,1<<6)||Mathf.Abs(hit.point.y-roof.position.y)>.025f)
                    throw new InvalidOperationException("Walkable roof mismatch "+name);
            }
            finally{Object.DestroyImmediate(building);}
        }
        report.platformRoofsValidated=true;report.success=true;
        File.WriteAllText(Source+"/unity-kit-validation.json",JsonUtility.ToJson(report,true));
        Debug.Log(JsonUtility.ToJson(report,true));
    }

    private static GameObject FindPrefab(string name)
    {
        foreach(string group in new[]{"Modules","Props","Weapons"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+group+"/"+name+".prefab");if(prefab!=null)return prefab;
        }
        throw new InvalidOperationException("Missing generated asset "+name);
    }

    public static void CaptureAll()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Capture();
        UrbanCharacterCapture.Capture();
        Debug.Log("URBAN_KIT_CAPTURE_SUCCESS");
    }

    public static void Capture()
    {
        bool asyncCompilation=ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation=false;
        var names=new[]{"Sword","Staff","StreetLamp","StreetTree","VendingMachine","Bench","TrashBin","ACUnit","GatePylon","Building_ShopLow","Building_OfficeMid","Building_ApartmentTall"};
        const int w=480,h=480;
        var sheet=new Texture2D(w*4,h*3,TextureFormat.RGB24,false);var target=new RenderTexture(w,h,24);
        var camera=new GameObject("Art Capture",typeof(Camera)).GetComponent<Camera>();camera.targetTexture=target;
        camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.79f,.84f,.88f);
        try
        {
            for(int i=0;i<names.Length;i++)
            {
                GameObject model=Object.Instantiate(FindPrefab(names[i]));model.transform.position=new Vector3(0,100,0);
                try
                {
                    Renderer[] renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(Renderer renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    camera.transform.position=bounds.center+new Vector3(1,.55f,-1.6f).normalized*40;
                    camera.transform.LookAt(bounds.center);camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x)*.7f;
                    camera.nearClipPlane=.01f;camera.farClipPlane=150;
                    camera.Render();camera.Render();RenderTexture.active=target;sheet.ReadPixels(new Rect(0,0,w,h),(i%4)*w,(2-i/4)*h);
                }
                finally{Object.DestroyImmediate(model);}
            }
            sheet.Apply();File.WriteAllBytes(Source+"/urban-kit-contact.png",sheet.EncodeToPNG());
        }
        finally{ShaderUtil.allowAsyncCompilation=asyncCompilation;RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(target);Object.DestroyImmediate(sheet);}
    }
}
