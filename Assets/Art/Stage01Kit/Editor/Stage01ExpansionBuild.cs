#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Additive authoring and review entry point. Does not regenerate the retained kit or gameplay scenes.
public static class Stage01ExpansionBuild
{
    const string Art="Assets/Art/Stage01Kit";
    const string Review="ArtSources/Stage01Kit/Expansion20261029";
    [Serializable] class Catalog {public Spec[] entries;public Spec[] retired_entries;}
    [Serializable] class Spec {public string id,name_ko,key,kind,status,prefab,role,placement;}
    [Serializable] class Entry {public string id,key;public int triangles,vertices,colliders,lodGroups;public Vector3 dimensions;public string[] issues;public string side_image,three_quarter_image;}
    [Serializable] class Audit {public string unity,created,method;public int total;public Entry[] entries;public string[] issues;public string[] connectionChecks;}
    static readonly List<string> checks=new List<string>();

    [MenuItem("Tools/Project M/Build Stage 1 Environment Expansion (21 Models)")]
    public static void Run()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        try {
            Directory.CreateDirectory(Review);checks.Clear();
            Stage01SimplifiedProps.Build();UrbanModuleBuilder.RebuildSideviewStairs();
            Stage01ExpansionBuildings.Build();Stage01ExpansionProps.Build();Stage01ExpansionDetails.Build();
            BuildBlocks();BuildScene(false);BuildScene(true);
            RemoveRetiredAssets();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateConnections();CaptureCatalog();CaptureSideviewSafety();
            Debug.Log("STAGE01_EXPANSION_SUCCESS: 21 expansion models; 54 active catalog entries; 3 retired IDs.");
        } catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void ReviewOnly(){checks.Clear();ValidateConnections();CaptureCatalog();}
    static GameObject Place(GameObject parent,string name,Vector3 p,string group="Expansion",float rotation=0)
    {
        string path=Art+"/Prefabs/"+group+"/"+name+".prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab)throw new FileNotFoundException(path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.transform);
        go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,rotation,0);return go;
    }
    static void Marker(GameObject root,string name,Vector3 p)=>S1Assets.Anchor(root,name,p);
    static void Gate(GameObject parent,Vector3 p,int number)
    {
        Marker(parent,"NormalGate_"+number,p);
    }
    static void BuildBlocks()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var street=new GameObject("Stage01_ExpandedStreet_Block");
        // Normal field: three distinct destinations at ground, low roof and high roof.
        for(int i=0;i<7;i++)Place(street,"Road_Straight_12m",new Vector3(-38+i*12,0,0),"Modules");
        Place(street,"Road_Crosswalk_6m",new Vector3(-38,.025f,0));
        Place(street,"Damage_CrackedRoad_6m",new Vector3(-30,.025f,0));
        Place(street,"Building_ConvenienceStore",new Vector3(-20,0,0));
        Place(street,"Exterior_Stairs_Rise4m",new Vector3(-28,0,0)); // exit(-24,4) aligns shop roof left.
        Place(street,"Exterior_Stairs_Rise4m",new Vector3(-12,4,0)); // shop right(-16,4) to cafe left(-8,8).
        Place(street,"Building_CornerCafe",new Vector3(-4,0,0));
        Place(street,"Exterior_Stairs_Rise4m",new Vector3(4,8,0)); // cafe right(0,8) to tall roof left(8,12).
        Place(street,"Building_MixedUseNarrow",new Vector3(11,0,0));
        Place(street,"Rooftop_Landing_4m",new Vector3(16,12,0));
        Place(street,"Rooftop_Landing_4m",new Vector3(20,8,0));
        Place(street,"Rooftop_Landing_4m",new Vector3(24,4,0));
        Place(street,"Ladder_4m",new Vector3(17.5f,8,0),"Modules");
        Place(street,"Ladder_4m",new Vector3(21.5f,4,0),"Modules");
        Place(street,"Ladder_4m",new Vector3(25.5f,0,0),"Modules");
        // Rear band: visual dressing and optional shop slice do not cover the front travel lane.
        Place(street,"Building_Rowhouse",new Vector3(25,0,7));
        Place(street,"Building_ParkingDeck",new Vector3(40,0,7));
        Place(street,"Shop_InteriorSlice_8m",new Vector3(32,0,0));
        Place(street,"Alley_Passage_6m",new Vector3(22,0,0));
        Place(street,"Rooftop_HVAC",new Vector3(-5,8,1.3f));
        Place(street,"Rooftop_DuctElbow",new Vector3(-1.5f,8,1.5f));
        Place(street,"SatelliteDish",new Vector3(12,12,1.45f),"Props");
        Place(street,"Street_BusShelter",new Vector3(-40,0,2.3f));
        Place(street,"Street_TrafficSignal",new Vector3(-35,0,2.5f));
        Place(street,"Street_UtilityPole",new Vector3(20,0,2.5f));
        for(int i=0;i<3;i++)Place(street,"Street_Bollard",new Vector3(-43+i*1.5f,0,1.6f));
        Place(street,"Prop_CargoPallet",new Vector3(26,0,1.5f));
        Place(street,"Prop_WasteBags",new Vector3(19.5f,0,1.5f));
        Place(street,"Prop_EmergencyBarricade",new Vector3(-32,0,1.5f));
        Place(street,"Damage_RubbleCluster",new Vector3(-30,0,2));
        Place(street,"StreetLamp",new Vector3(-44,0,2.3f),"Props");
        Place(street,"StreetTree",new Vector3(38,0,2.6f),"Props");
        Place(street,"VendingMachine",new Vector3(28,0,2),"Props");
        Gate(street,new Vector3(-33,0,0),1);Gate(street,new Vector3(-20,4,0),2);Gate(street,new Vector3(11,12,0),3);
        Marker(street,"PlayerSpawn",new Vector3(-44,0,0));Marker(street,"OptionalItem_Interior",new Vector3(34,0,0));
        Marker(street,"ReturnToMainRoute",new Vector3(27,0,0));Marker(street,"BossNodeTransfer",new Vector3(40,0,0));
        PrefabUtility.SaveAsPrefabAsset(street,Art+"/Prefabs/Blocks/Stage01_ExpandedStreet_Block.prefab");Object.DestroyImmediate(street);
        var boss=new GameObject("Stage01_ExpandedBoss_Block");
        // Boss remains a separate map-node composition, with a clear central fighting lane.
        Place(boss,"Building_RooftopArena",new Vector3(0,8,0),"Modules");
        Place(boss,"Rooftop_HVAC",new Vector3(-5,12,2.5f));
        Place(boss,"Rooftop_DuctElbow",new Vector3(-1,12,2.6f));
        Place(boss,"SatelliteDish",new Vector3(7.5f,12,2.5f),"Props");
        for(int i=0;i<4;i++)Place(boss,"Guardrail_4m",new Vector3(-6+i*4,12,2.8f),"Modules");
        Place(boss,"Damage_RubbleCluster",new Vector3(7,12,2));
        Marker(boss,"PlayerSpawn",new Vector3(-7,12,0));Marker(boss,"BossArenaAnchor",new Vector3(0,12,0));
        Marker(boss,"BossGateAnchor",new Vector3(4,12,0));Marker(boss,"ReturnNodeAnchor",new Vector3(-8,12,0));
        PrefabUtility.SaveAsPrefabAsset(boss,Art+"/Prefabs/Blocks/Stage01_ExpandedBoss_Block.prefab");Object.DestroyImmediate(boss);
    }
    static void BuildScene(bool boss)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        string key=boss?"Stage01_ExpandedBoss_Block":"Stage01_ExpandedStreet_Block";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/Blocks/"+key+".prefab");PrefabUtility.InstantiatePrefab(prefab);
        SetupLighting();var c=new GameObject("ArtPreview_Camera").AddComponent<Camera>();c.tag="MainCamera";c.orthographic=true;
        c.orthographicSize=boss?11:26;c.transform.position=(boss?new Vector3(0,12,0):new Vector3(-3,6,0))+new Vector3(1,12,-80);
        c.transform.LookAt(boss?new Vector3(0,12,0):new Vector3(-3,6,0));c.nearClipPlane=.1f;c.farClipPlane=300;
        c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.105f,.13f,.17f);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Art+"/"+(boss?"Stage01_BossArt_Preview":"Stage01_Expansion_Preview")+".unity");
    }
    static void SetupLighting()
    {
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.67f,.74f);RenderSettings.ambientIntensity=1;
        RenderSettings.fog=false;var sun=new GameObject("Overcast_Key").AddComponent<Light>();sun.type=LightType.Directional;
        sun.intensity=1.25f;sun.color=new Color(1,.94f,.84f);sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
    }
    static void RemoveRetiredAssets()
    {
        foreach(var key in new[]{"Rooftop_Antenna","Gate_Platform_Normal","Gate_Platform_Boss"}){
            foreach(var path in new[]{Art+"/Prefabs/Expansion/"+key+".prefab",Art+"/Meshes/Expansion/"+key+".asset"})
                if(AssetDatabase.LoadMainAssetAtPath(path)&&!AssetDatabase.DeleteAsset(path))throw new IOException("Could not remove retired asset: "+path);
        }
        foreach(var id in new[]{"S1-042","S1-052","S1-053"})foreach(var view in new[]{"side","perspective"}){
            string path=Review+"/Renders/"+id+"_"+view+".png";if(File.Exists(path))File.Delete(path);
        }
        // Keep the original generated source for provenance outside Unity's active model folder.
        string oldSource=Art+"/Models/GatePylon.fbx";
        if(File.Exists(oldSource)){
            string archive="ArtSources/Stage01Kit/LegacyMeshySources";Directory.CreateDirectory(archive);
            string destination=archive+"/GatePylon_original.fbx";
            if(!File.Exists(destination))File.Copy(oldSource,destination);
            if(File.Exists(oldSource+".meta")&&!File.Exists(destination+".meta.txt"))File.Copy(oldSource+".meta",destination+".meta.txt");
            if(!AssetDatabase.DeleteAsset(oldSource))throw new IOException("Could not retire original GatePylon FBX.");
        }
    }
    static void ValidateConnections()
    {
        var stairs=AssetDatabase.LoadAssetAtPath<GameObject>(S1Assets.Prefabs+"/Exterior_Stairs_Rise4m.prefab");
        if((stairs.transform.Find("Exit").localPosition-stairs.transform.Find("Entry").localPosition-new Vector3(8,4,0)).sqrMagnitude>1e-6f)throw new Exception("Stair anchor mismatch.");
        var boxes=stairs.GetComponentsInChildren<BoxCollider>();if(boxes.Length!=16)throw new Exception("Expected 16 stair colliders.");
        for(int i=0;i<16;i++)if(Mathf.Abs(boxes[i].center.y+boxes[i].size.y*.5f-(i+1)*.25f)>1e-5f)throw new Exception("Stair tread mismatch.");
        checks.Add("16 treads and Entry/Exit match 8m run / 4m rise.");
        foreach(var key in new[]{"Building_ConvenienceStore","Building_CornerCafe","Building_MixedUseNarrow","Building_Rowhouse","Building_ParkingDeck"}){
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(S1Assets.Prefabs+"/"+key+".prefab");var box=p.GetComponentInChildren<BoxCollider>();var roof=p.transform.Find("RoofSurface");
            if(Mathf.Abs(roof.localPosition.y-(box.center.y+box.size.y*.5f))>1e-5f)throw new Exception("Roof collider mismatch "+key);
        }
        checks.Add("Five new solid building roofs match Ground collider heights (4/8/12m).");
        var normal=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/Blocks/Stage01_ExpandedStreet_Block.prefab");
        for(int i=1;i<=3;i++)if(!normal.transform.Find("NormalGate_"+i))throw new Exception("Missing normal gate anchor.");
        if(normal.transform.Find("BossGateAnchor"))throw new Exception("Boss gate must stay in separate node.");
        checks.Add("Normal composition has exactly three numbered gate anchors; boss composition is separate.");
        var three=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/Modules/Exterior_Stairs_Rise3m.prefab");
        var mesh=three.GetComponentsInChildren<MeshFilter>().Single().sharedMesh;
        // Above the ascending tread envelope, all rail vertices must be on the rear (+Z) side.
        if(mesh.vertices.Any(v=>v.z<-.6f&&v.y>(v.x+2)*.75f+.32f))throw new Exception("S1-008 front rail still obscures player lane.");
        checks.Add("S1-008 has no camera-front handrail/posts above the tread envelope; rear rail retained.");
        foreach(var key in new[]{"Rooftop_Antenna","Gate_Platform_Normal","Gate_Platform_Boss"})
            if(AssetDatabase.LoadAssetAtPath<GameObject>(S1Assets.Prefabs+"/"+key+".prefab"))throw new Exception("Retired model still active: "+key);
        checks.Add("Retired model prefabs removed; regeneration cannot recreate them.");
    }
    static void CaptureCatalog()
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("Render verification requires graphics.");
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Review+"/catalog.json"));
        if(catalog.entries.Length!=54||catalog.entries.Select(x=>x.id).Distinct().Count()!=54||catalog.entries.Any(x=>new[]{"S1-042","S1-052","S1-053"}.Contains(x.id)))throw new Exception("Catalog IDs invalid.");
        var allIssues=new List<string>();var entries=new List<Entry>();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SetupLighting();
        bool previousAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var camera=new GameObject("CatalogCamera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.02f;camera.farClipPlane=1500;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.105f,.13f,.17f);camera.allowHDR=false;camera.allowMSAA=true;
        var rt=new RenderTexture(768,640,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();camera.targetTexture=rt;camera.aspect=768f/640;
        Directory.CreateDirectory(Review+"/Renders");
        try {foreach(var spec in catalog.entries){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefab);if(!prefab)throw new FileNotFoundException(spec.prefab);
            var go=Object.Instantiate(prefab);go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;
            foreach(var b in go.GetComponentsInChildren<MonoBehaviour>(true))if(b)b.enabled=false;
            foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var lods=go.GetComponentsInChildren<LODGroup>(true);var excluded=new HashSet<Renderer>();
            foreach(var l in lods){l.ForceLOD(0);foreach(var level in l.GetLODs().Skip(1))foreach(var r in level.renderers)excluded.Add(r);}
            var renderers=go.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!excluded.Contains(r)).ToArray();
            var issues=new List<string>();if(renderers.Length==0)issues.Add("No renderers");
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            int triangles=0,vertices=0;
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true)){
                foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader))issues.Add("Missing/unsupported shader "+r.name);
                var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;if(!mesh){issues.Add("Missing mesh "+r.name);continue;}
                var v=mesh.vertices;var ix=mesh.triangles;
                if(!excluded.Contains(r)){triangles+=ix.Length/3;vertices+=v.Length;}
                if(v.Any(p=>float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z)))issues.Add("Non-finite vertices");
                if(ix.Any(i=>i<0||i>=v.Length))issues.Add("Invalid indices");
                if(mesh.colors.Length!=v.Length)issues.Add("Missing vertex colors");
                for(int i=0;i<ix.Length;i+=3)if(Vector3.Cross(v[ix[i+1]]-v[ix[i]],v[ix[i+2]]-v[ix[i]]).sqrMagnitude<1e-16f){issues.Add("Degenerate triangle in "+mesh.name);break;}
            }
            foreach(var t in go.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)issues.Add("Missing script "+t.name);
            string side="Renders/"+spec.id+"_side.png",perspective="Renders/"+spec.id+"_perspective.png";
            Capture(camera,rt,bounds,new Vector3(.06f,.1f,-1),Review+"/"+side);
            Capture(camera,rt,bounds,new Vector3(.65f,.38f,-1),Review+"/"+perspective);
            entries.Add(new Entry{id=spec.id,key=spec.key,triangles=triangles,vertices=vertices,dimensions=bounds.size,colliders=go.GetComponentsInChildren<Collider>(true).Length,lodGroups=lods.Length,issues=issues.Distinct().ToArray(),side_image=side,three_quarter_image=perspective});
            allIssues.AddRange(issues.Select(x=>spec.id+": "+x));Object.DestroyImmediate(go);Debug.Log("S1_CATALOG_RENDERED "+spec.id+" "+spec.key);
        }} finally {camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);ShaderUtil.allowAsyncCompilation=previousAsync;}
        var audit=new Audit{unity=Application.unityVersion,created="2026-10-29",total=entries.Count,entries=entries.ToArray(),issues=allIssues.Distinct().ToArray(),connectionChecks=checks.ToArray(),method="Actual Unity-prefab renders, retained shaders, LOD0 shown/counts; all LOD geometry checked. Static art and anchor checks, not runtime traversal certification."};
        File.WriteAllText(Review+"/audit.json",JsonUtility.ToJson(audit,true));
        if(allIssues.Count>0)throw new Exception("Catalog structural issues: "+string.Join("; ",audit.issues));
    }
    static void Capture(Camera camera,RenderTexture rt,Bounds b,Vector3 dir,string path)
    {
        camera.transform.position=b.center+dir.normalized*(b.size.magnitude*2+10);camera.transform.LookAt(b.center);
        float w=0,h=0;foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1}){
            var p=Vector3.Scale(b.extents,new Vector3(x,y,z));w=Mathf.Max(w,Mathf.Abs(Vector3.Dot(p,camera.transform.right)));h=Mathf.Max(h,Mathf.Abs(Vector3.Dot(p,camera.transform.up)));}
        camera.orthographicSize=Mathf.Max(.08f,Mathf.Max(h,w/camera.aspect)*1.16f);camera.Render();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=previous;
    }
    static void CaptureSideviewSafety()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SetupLighting();
        var stairs=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/Modules/Exterior_Stairs_Rise3m.prefab"));
        var heroPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Protagonist01/Combat/Prefabs/Protagonist_Combat.prefab");
        if(!heroPrefab)throw new FileNotFoundException("Current protagonist required for stair visual review.");
        var hero=Object.Instantiate(heroPrefab);
        foreach(var b in hero.GetComponentsInChildren<MonoBehaviour>(true))if(b)b.enabled=false;
        foreach(var a in hero.GetComponentsInChildren<Animator>(true))a.enabled=false;
        hero.transform.rotation=Quaternion.Euler(0,90,0);
        var rr=hero.GetComponentsInChildren<Renderer>(true);var hb=rr[0].bounds;foreach(var r in rr.Skip(1))hb.Encapsulate(r.bounds);
        hero.transform.localScale*=1.65f/hb.size.y;
        hb=rr[0].bounds;foreach(var r in rr.Skip(1))hb.Encapsulate(r.bounds);
        hero.transform.position+=new Vector3(-hb.center.x,-hb.min.y,-hb.center.z);
        var origin=hero.transform.position;
        var c=new GameObject("SafetyCamera").AddComponent<Camera>();c.orthographic=true;c.nearClipPlane=.02f;c.farClipPlane=100;
        c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.105f,.13f,.17f);
        var rt=new RenderTexture(1200,900,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();c.targetTexture=rt;c.aspect=1200f/900;
        var bounds=new Bounds(new Vector3(0,2.25f,0),new Vector3(5.2f,5.3f,2));
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try{
            for(int i=0;i<3;i++){
                // These X values sit in the centers of treads 3, 7 and 11.
                float x=-2+(new[]{2,6,10}[i]+.5f)/3f,y=(new[]{2,6,10}[i]+1)*.25f;
                hero.transform.position=origin+new Vector3(x,y,0);
                Capture(c,rt,bounds,new Vector3(.06f,.1f,-1),Review+"/Renders/S1-008_character_"+(i+1)+".png");
            }
        }finally{c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);ShaderUtil.allowAsyncCompilation=async;Object.DestroyImmediate(hero);Object.DestroyImmediate(stairs);}
    }
}
#endif
