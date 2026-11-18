using System;
using System.IO;
using System.Linq;
using OZ.UI.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Rebuilds only stage-authored geometry, visuals and serialized encounter data.</summary>
public static class Stage01LevelBuilder
{
    public const string StagePath="Assets/2.Prefab/CoreSystem/stage.prefab";
    public const string MapPath="Assets/04.Data/Map_Stage01_Runtime.asset";
    const string Kit="Assets/Art/Stage01Kit/Prefabs/";
    const string Samples="Assets/02.parkhansol_/UI/Data/Samples/";
    const string Mats="Assets/Art/Stage01Polish/Materials/";
    static Material cream,clay,dark,teal,gold,brick,paving,sky,haze;
    [MenuItem("OZ/Level Design/Rebuild And Validate")]
    public static void BuildAndValidate(){Build();Stage01VerticalAssetQA.Run();}
    [MenuItem("OZ/Level Design/Rebuild Validate And Play")]
    public static void BuildValidateAndPlay(){BuildAndValidate();Stage01VerticalQA.Start();}
    [MenuItem("OZ/Level Design/Rebuild Stage 01")]
    public static void Build()
    {
        Stage01ArtFactory.EnsureAssets();Directory.CreateDirectory(Mats);
        cream=Mat("Plaster",new Color(.90f,.79f,.59f));clay=Mat("Terracotta",new Color(.69f,.32f,.23f));
        dark=Mat("InkStructure",new Color(.12f,.20f,.25f));teal=Mat("PaintedTeal",new Color(.17f,.46f,.46f));
        gold=Mat("RouteOchre",new Color(.96f,.68f,.28f));brick=Mat("Foundation",new Color(.30f,.32f,.32f));
        paving=Mat("Paving",new Color(.43f,.47f,.45f));haze=Mat("Distance",new Color(.49f,.58f,.65f),.25f);
        sky=Mat("Sky",Color.white);
        sky.shader=Shader.Find("ProjectM/Stage01/Sky Atmosphere");
        sky.SetColor("_HorizonColor",new Color(.70f,.84f,.81f));sky.SetColor("_ZenithColor",new Color(.34f,.57f,.74f));
        sky.SetFloat("_HorizonY",-12);sky.SetFloat("_ZenithY",18);EditorUtility.SetDirty(sky);
        GameObject root=new GameObject("Stage01_Rift_Over_Seoul");Stage stage=root.AddComponent<Stage>();
        Transform route=Group(root.transform,"01_Playable_Street_And_Rooftops"),city=Group(root.transform,"02_Anchored_City_And_Sky"),
            details=Group(root.transform,"03_Quiet_Environmental_Stories"),encounters=Group(root.transform,"04_Rifts_And_Visible_Seals"),
            supplies=Group(root.transform,"05_Supply_Detours"),rooms=Group(root.transform,"06_Map_Rooms");
        Transform spawn=Group(root.transform,"PlayerSpawn");spawn.localPosition=new Vector3(-6,.05f,0);spawn.localRotation=Quaternion.LookRotation(Vector3.right);
        StageLayout layout=root.AddComponent<StageLayout>();layout.Configure(spawn,BuildMap(),new Vector2(-12,116));
        Set(layout,"cameraSize",p=>p.floatValue=7);Set(layout,"cameraHeight",p=>p.floatValue=3.5f);Set(layout,"cameraPitch",p=>p.floatValue=6.5f);
        Box("Street_Surface_No_Seams",route,new Vector3(52,-.32f,0),new Vector3(128,.64f,6),6);
        Block("Street_Cutaway_Foundation",route,new Vector3(52,-2.15f,1),new Vector3(128,3.7f,10),brick);
        for(int i=0;i<11;i++)Art("Modules/Road_Straight_12m",route,new Vector3(-6+i*12,-.015f,0),"Teal",false);
        Block("Front_Curb_Ink",route,new Vector3(52,-.08f,-2.8f),new Vector3(128,.18f,.20f),dark);
        Block("Front_Curb_Cap",route,new Vector3(52,.015f,-2.8f),new Vector3(128,.04f,.23f),cream);
        for(int i=0;i<22;i++)Block("Curb_Joint",details,new Vector3(-10+i*6,-.11f,-2.92f),new Vector3(.025f,.23f,.025f),dark);
        for(int i=0;i<16;i++)Art("Modules/Sidewalk_Straight_8m",city,new Vector3(-8+i*8,.025f,4),"Clay",false);
        Box("West_Perimeter",route,new Vector3(-12.35f,12,0),new Vector3(.7f,28,6),0);
        Box("East_Perimeter",route,new Vector3(116.35f,12,0),new Vector3(.7f,28,6),0);
        Deck(route,26.4f,32.8f,2.25f,"Market_Awning_Step",true);
        Deck(route,34,58.5f,4.5f,"Cafe_Roof_Arena",false);
        Deck(route,59.2f,64.5f,6.5f,"Service_Bridge_Step",true);
        Deck(route,66,88.5f,8.5f,"Garden_Roof_Arena",false);
        Deck(route,88.8f,93.5f,4.5f,"Visible_Descent_Terrace",true);
        Deck(route,39,42,6.9f,"Cafe_Combat_Perch",true,4.5f);
        Deck(route,80,83,10.9f,"Garden_Combat_Perch",true,8.5f);
        LadderAt(route,33.5f,4.5f,34.5f,"Cafe_Recovery_Ladder");LadderAt(route,65.5f,8.5f,66.5f,"Garden_Recovery_Ladder");
        Facade(city,46.25f,24.5f,4.5f,clay,"Cafe_Row");Facade(city,77.25f,22.5f,8.5f,cream,"Garden_Atelier");
        Facade(city,91.15f,4.7f,4.5f,teal,"Stair_House");Facade(city,29.6f,6.4f,2.25f,teal,"Market_Kiosk");
        Parapet(details,34,58.5f,4.5f);Parapet(details,66,88.5f,8.5f);
        for(int i=0;i<4;i++)Art("Modules/Planter_Shrubs",details,new Vector3(68+i*5.5f,8.52f,2.8f),"Foliage");
        Art("Expansion/Rooftop_HVAC",details,new Vector3(54.5f,4.52f,3.7f),"Teal");
        Art("Modules/Rooftop_WaterTank",details,new Vector3(84,8.52f,4.2f),"Ochre");
        Art("Expansion/Rooftop_DuctElbow",details,new Vector3(70,8.52f,4),"Teal");
        Art("Props/Bench",details,new Vector3(91.5f,4.52f,2.1f),"Clay");
        Landing(details,27.2f,2.25f);Landing(details,35.3f,4.5f);Landing(details,60.2f,6.5f);
        Landing(details,67.2f,8.5f);Landing(details,90.4f,4.5f);Landing(details,95.5f,0);
        Background(city,details);
        Gate first=Encounter(encounters,"01_Market_Rift",14,0,3,.65f,new[]{"Monster_Skeleton1"},new[]{9f,13f,18f,22f});
        Gate second=Encounter(encounters,"02_Cafe_Rooftop_Rift",45,4.5f,4,.55f,new[]{"Monster_Skeleton1","Monster_Ranged1"},new[]{38f,42f,48f,53f});
        Gate third=Encounter(encounters,"03_Garden_Rooftop_Rift",76,8.5f,6,.5f,new[]{"Monster_Skeleton1","Monster_Ranged1","Monster_Charge1"},new[]{69.5f,73.5f,79f,84f});
        Seal(encounters,first,26,0);Seal(encounters,second,58,4.5f);Seal(encounters,third,88,8.5f);
        BossGate boss=Boss(encounters);Set(stage,"bossGate",p=>p.objectReferenceValue=boss);
        Set(stage,"gates",p=>{p.arraySize=3;p.GetArrayElementAtIndex(0).objectReferenceValue=first;p.GetArrayElementAtIndex(1).objectReferenceValue=second;p.GetArrayElementAtIndex(2).objectReferenceValue=third;});
        Deck(supplies,29,32.2f,5,"Optional_Market_Lookout",true);
        Cache(supplies,"Market_Lookout_Cache",new Vector3(30.6f,5.04f,0),new[]{"Heal","Attack"});
        Cache(supplies,"Service_Bridge_Cache",new Vector3(62.5f,6.54f,0),new[]{"Heal"});
        Cache(supplies,"PreBoss_Recovery",new Vector3(97,.04f,0),new[]{"Heal"});
        Art("Props/StreetLamp",details,new Vector3(30.6f,5,2.3f),"Ochre");
        Room(rooms,"entry","서울 균열 지구","STAGE 01 · 시장에서 옥상으로",-12,3,-1,18);
        Room(rooms,"market","오후의 시장 거리","첫 번째 균열 · 거리 전투",3,26,-1,18);
        Room(rooms,"ascent","카페 옥상으로","금빛 발판을 따라 위로",26,34,-1,4.7f);
        Room(rooms,"cache_west","시장 위 전망대","선택 경로 · 보급품",28,33,4.7f,18);
        Room(rooms,"crossing","카페의 연결된 지붕","두 번째 균열 · 옥상 전투",34,58.6f,-1,22);
        Room(rooms,"cache_east","설비 연결교","보급 후 더 높은 정원으로",58.6f,66,-1,22);
        Room(rooms,"lockdown","하늘 정원","세 번째 균열 · 혼합전",66,88.6f,-1,26);
        Room(rooms,"descent","광장으로 내려가는 길","아래쪽 금빛 착지점으로",88.6f,94,-1,26);
        Room(rooms,"boss","균열 파수꾼의 광장","보스 게이트를 공격해 개방",94,116,-1,26);
        PrefabUtility.SaveAsPrefabAsset(root,StagePath);UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        Debug.Log("[Stage01] Vertical street/cafe/garden route, anchored city, new rifts and fallback saved.");
    }
    static void Background(Transform city,Transform details)
    {
        Block("Quiet_Sky",city,new Vector3(52,30,69),new Vector3(240,100,1),sky);
        Block("City_Ground_Mass",city,new Vector3(52,-4,59),new Vector3(230,8,20),haze);
        Block("Rear_Street_Paving",city,new Vector3(52,-.12f,14),new Vector3(190,.28f,24),paving);
        // Distant towers emerge from a continuous district, never expose a cut bottom in sky.
        Material district=Mat("District_Plaster",new Color(.396f,.51f,.545f));district.shader=Shader.Find("Universal Render Pipeline/Unlit");
        district.SetColor("_BaseColor",new Color(.396f,.51f,.545f));EditorUtility.SetDirty(district);
        Block("Far_District_Continuous_Base",city,new Vector3(52,-3.5f,25),new Vector3(190,8,2),district);
        Material cloud=Mat("Cloud",new Color(.90f,.88f,.76f));cloud.shader=Shader.Find("Universal Render Pipeline/Unlit");cloud.SetColor("_BaseColor",new Color(.90f,.88f,.76f));EditorUtility.SetDirty(cloud);
        for(int i=0;i<5;i++)
        {
            float x=-12+i*34,y=1+(i%3)*2.5f;
            foreach(Vector3 lobe in new[]{new Vector3(-3,0,4.8f),new Vector3(0,.4f,6.8f),new Vector3(3.5f,-.05f,4.8f)})
            {
                GameObject c=GameObject.CreatePrimitive(PrimitiveType.Sphere);c.name="Quiet_Cloud_Silhouette";c.transform.SetParent(city,false);
                c.transform.localPosition=new Vector3(x+lobe.x,y+lobe.y,64);c.transform.localScale=new Vector3(lobe.z,1.1f+lobe.y,.3f);
                UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());c.GetComponent<Renderer>().sharedMaterial=cloud;c.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        Block("Rear_Park_Retaining_Wall",city,new Vector3(52,.4f,8.4f),new Vector3(150,.8f,.4f),teal);
        Block("Rear_Park_Wall_Cap",city,new Vector3(52,.84f,8.4f),new Vector3(150,.08f,.5f),cream);
        for(int i=0;i<12;i++){GameObject b=Art(i%2==0?"Modules/Building_ApartmentTall":"Modules/Building_OfficeMid",city,new Vector3(-22+i*14,-4.2f,32+(i%3)*3),"Far",false);b.transform.localScale=new Vector3(.85f,.95f+(i%4)*.18f,.85f);}
        string[] buildings={"Expansion/Building_CornerCafe","Expansion/Building_ConvenienceStore","Expansion/Building_Rowhouse","Modules/Building_ShopLow","Expansion/Building_MixedUseNarrow"};
        float[] xs={-4,17,42,63,101};
        for(int i=0;i<xs.Length;i++){Art(buildings[i],city,new Vector3(xs[i],-.15f,11.5f),i%2==0?"Clay":"Teal");Block("Building_Grounded_Plinth",city,new Vector3(xs[i],-.24f,10.4f),new Vector3(14,.5f,9),brick);}
        foreach(float x in new[]{-9f,1f,24f,96f,114f}){Art("Props/StreetLamp",details,new Vector3(x,.03f,4.1f),"Ochre");Art("Modules/Planter_Shrubs",details,new Vector3(x+1.5f,.03f,5.5f),"Foliage");}
        foreach(float x in new[]{-8f,28f,93.5f,113f})Art("Props/StreetTree",details,new Vector3(x,.03f,7.4f),"Foliage");
        Art("Props/Bench",details,new Vector3(-2,.03f,4.6f),"Clay");Art("Props/TrashBin",details,new Vector3(.6f,.03f,4.8f),"Teal");
        Art("Props/VendingMachine",details,new Vector3(99,.03f,5.2f),"Clay");Art("Expansion/Street_BusShelter",details,new Vector3(109,.03f,6.8f),"Teal");
        Art("Expansion/Prop_CargoPallet",details,new Vector3(8,.03f,5.3f),"Ochre");Art("Expansion/Street_TrafficSignal",details,new Vector3(22,.03f,5.5f),"Teal");
        Art("Expansion/Road_Crosswalk_6m",city,new Vector3(101,.014f,0),"Clay",false);
        Block("Plaza_Retaining_Wall",city,new Vector3(105,.5f,8.2f),new Vector3(23,1,1),cream);Block("Plaza_Wall_Cap",city,new Vector3(105,1.04f,8.2f),new Vector3(23,.12f,1.1f),dark);
    }
    static void Facade(Transform parent,float x,float width,float roof,Material plaster,string name)
    {
        Transform f=Group(parent,name+"_Load_Bearing_Building");
        Block("Plinth",f,new Vector3(x,.24f,3.8f),new Vector3(width,.48f,5.1f),dark);
        Block("Wall",f,new Vector3(x,(roof-.35f)*.5f,3.8f),new Vector3(width,roof-.35f,5),plaster);
        Block("Cornice_Shadow",f,new Vector3(x,roof-.3f,3.7f),new Vector3(width+.18f,.28f,5.2f),dark);
        Block("Cornice_Cream",f,new Vector3(x,roof-.15f,3.7f),new Vector3(width+.25f,.16f,5.28f),cream);
        // Extend the visual roof behind the play deck to support all rear props at the same elevation.
        Block("Rear_Roof_Continuous_Surface",f,new Vector3(x,roof-.025f,4.5f),new Vector3(width,.05f,3.6f),cream);
        int bays=Mathf.Max(1,Mathf.FloorToInt(width/3.7f)),rows=Mathf.Max(1,Mathf.FloorToInt((roof-2.45f)/2.7f)+1);
        float windowHeight=roof<3?1.05f:1.5f;
        for(int row=0;row<rows;row++)for(int i=0;i<bays;i++){float wx=x-width*.5f+(i+.5f)*width/bays,wy=(roof<3?1.1f:1.7f)+row*2.7f-(row==2?.2f:0);
            Block("Window_Deep_Reveal",f,new Vector3(wx,wy,1.245f),new Vector3(1.7f,windowHeight,.15f),dark);
            Block("Window_Teal_Glass",f,new Vector3(wx,wy+.04f,1.15f),new Vector3(1.43f,windowHeight-.3f,.07f),teal);
            Block("Window_Mullion",f,new Vector3(wx,wy+.04f,1.09f),new Vector3(.07f,windowHeight-.3f,.04f),cream);
            Block("Window_Sill",f,new Vector3(wx,wy-windowHeight*.5f+.02f,1.08f),new Vector3(1.95f,.13f,.32f),cream);}
        for(float c=x-width*.5f+.3f;c<x+width*.5f;c+=width/Mathf.Max(1,bays))Block("Masonry_Pier",f,new Vector3(c,roof*.46f,1.2f),new Vector3(.20f,roof*.88f,.22f),cream);
        if(width>10){Block("Painted_Sign_Ink",f,new Vector3(x,3.3f,1.03f),new Vector3(width*.42f,.62f,.16f),dark);Block("Painted_Sign",f,new Vector3(x,3.3f,.93f),new Vector3(width*.40f,.44f,.07f),gold);
            for(int i=-2;i<=2;i++)Block("Sign_Graphic",f,new Vector3(x+i*.85f,3.3f,.88f),new Vector3(.4f,.11f,.02f),dark);}
    }
    static void Deck(Transform p,float left,float right,float top,string name,bool oneWay,float supportBase=0)
    {
        Transform d=Group(p,name);d.localPosition=new Vector3((left+right)*.5f,top,0);d.gameObject.layer=6;
        BoxCollider c=d.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,-.18f,.5f);c.size=new Vector3(right-left,.36f,4.4f);if(oneWay)d.gameObject.AddComponent<OneWayPlatform>();
        Block("Roof_Ink_Slab",d,new Vector3(0,-.215f,.5f),new Vector3(right-left,.37f,4.4f),dark);Block("Walkable_Sandstone_Top",d,new Vector3(0,-.015f,.5f),new Vector3(right-left,.03f,4.4f),cream);
        Block("Readable_Gold_Edge",d,new Vector3(0,-.08f,-1.73f),new Vector3(right-left,.13f,.1f),gold);
        if(oneWay)foreach(float x in new[]{left+.3f,right-.3f}){Block("Visible_Support_Post",p,new Vector3(x,(top+supportBase)*.5f,2.4f),new Vector3(.18f,top-supportBase,.18f),dark);Block("Support_Foot",p,new Vector3(x,supportBase+.16f,2.4f),new Vector3(.46f,.32f,.46f),brick);}
    }
    static void Parapet(Transform p,float l,float r,float y)
    {Block("Rear_Parapet",p,new Vector3((l+r)*.5f,y+.4f,3.05f),new Vector3(r-l,.8f,.35f),teal);Block("Rear_Parapet_Cap",p,new Vector3((l+r)*.5f,y+.85f,3.05f),new Vector3(r-l,.1f,.46f),cream);
        for(float x=l+.4f;x<r;x+=4)Block("Rear_Parapet_Pillar",p,new Vector3(x,y+.48f,3.05f),new Vector3(.3f,.96f,.5f),cream);}
    static void LadderAt(Transform p,float x,float h,float exitX,string name)
    {
        Transform t=Group(p,name);t.localPosition=new Vector3(x,0,0);BoxCollider c=t.gameObject.AddComponent<BoxCollider>();c.isTrigger=true;c.center=new Vector3(0,h*.5f,0);c.size=new Vector3(1.15f,h+1.2f,1.4f);
        Transform exit=Group(t,"TopExit_Clear_Landing");exit.position=new Vector3(exitX,h+.05f,0);Set(t.gameObject.AddComponent<Ladder>(),"_topExit",a=>a.objectReferenceValue=exit);
        foreach(float dx in new[]{-.33f,.33f}){Block("Ladder_Ink_Rail",t,new Vector3(dx,h*.5f,.8f),new Vector3(.13f,h+.5f,.15f),dark);Block("Ladder_Gold_Rail",t,new Vector3(dx,h*.5f,.7f),new Vector3(.065f,h+.5f,.05f),gold);}
        for(float y=.28f;y<h+.3f;y+=.38f)Block("Ladder_Rung",t,new Vector3(0,y,.72f),new Vector3(.73f,.08f,.12f),gold);
        Block("Ladder_Base_Pad",t,new Vector3(0,.014f,0),new Vector3(1.2f,.028f,1.6f),gold);
    }
    static void Landing(Transform p,float x,float y)
    {foreach(float dx in new[]{-.28f,.28f}){GameObject b=Block("Landing_Chevron",p,new Vector3(x+dx,y+.011f,-.65f),new Vector3(.45f,.018f,.08f),gold);b.transform.localRotation=Quaternion.Euler(0,dx<0?-30:30,0);}}
    static Gate Encounter(Transform p,string name,float x,float y,int count,float interval,string[] types,float[] points)
    {
        GameObject obj=Instance("Assets/2.Prefab/CoreSystem/Gate.prefab",p,new Vector3(x,y,0));obj.name=name;ReplacePortal(obj,false);
        foreach(Collider c in obj.GetComponentsInChildren<Collider>(true))c.isTrigger=true;Gate g=obj.GetComponent<Gate>();g.ConfigureEncounter(count,interval,10,1.5f,types);
        MonsterSpawner s=obj.GetComponentInChildren<MonsterSpawner>();if(s==null)s=obj.AddComponent<MonsterSpawner>();Set(g,"spawner",a=>a.objectReferenceValue=s);
        s.ConfigureSpawnPoints(points.Select((px,i)=>{Transform t=Group(p,name+"_Spawn_"+i);t.localPosition=new Vector3(px,y,0);return t;}).ToArray());return g;
    }
    static BossGate Boss(Transform p)
    {
        GameObject obj=Instance("Assets/2.Prefab/CoreSystem/BossGate.prefab",p,new Vector3(104,0,0));obj.name="04_Boss_Rift";ReplacePortal(obj,true);
        obj.layer=LayerMask.NameToLayer("Enemy"); // This damageable stage object must match existing weapon target masks.
        foreach(Collider c in obj.GetComponentsInChildren<Collider>(true))c.isTrigger=true;BossGate g=obj.GetComponent<BossGate>();Set(g,"summonDelay",a=>a.floatValue=2);
        MonsterSpawner s=obj.GetComponentInChildren<MonsterSpawner>();if(s==null)s=obj.AddComponent<MonsterSpawner>();
        s.ConfigureSpawnPoints(new[]{102f,109f}.Select((x,i)=>{Transform t=Group(p,"BossSpawn_Telegraphed_"+i);t.localPosition=new Vector3(x,0,0);
            foreach(float e in new[]{-.95f,.95f}){Block("Boss_Arrival_Outline",p,new Vector3(x+e,.018f,0),new Vector3(.06f,.025f,1.9f),gold);Block("Boss_Arrival_Outline",p,new Vector3(x,.018f,e),new Vector3(1.9f,.025f,.06f),gold);}return t;}).ToArray());
        Set(g,"spawner",a=>a.objectReferenceValue=s);return g;
    }
    static void ReplacePortal(GameObject obj,bool boss)
    {
        PrefabUtility.UnpackPrefabInstance(obj,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(Transform t in obj.transform.Cast<Transform>().ToArray())if(t.name=="Protal"||t.name=="portal")UnityEngine.Object.DestroyImmediate(t.gameObject);
        GameObject a=Stage01ArtFactory.CreatePortalVisual(obj.transform,boss);a.transform.localPosition=new Vector3(0,0,1.45f);StageGate g=obj.GetComponent<StageGate>();
        Set(g,"gateRenderer",p=>p.objectReferenceValue=a.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="StatusGem"));Set(g,"gateMaterial",p=>p.objectReferenceValue=null);
        Set(g,"animController",p=>p.objectReferenceValue=a.GetComponentInChildren<GateAnimController>(true));
    }
    static void Seal(Transform p,Gate g,float x,float y)
    {
        Transform t=Group(p,"ExitSeal_"+g.name);t.localPosition=new Vector3(x,0,0);BoxCollider c=t.gameObject.AddComponent<BoxCollider>();c.size=new Vector3(.3f,28,6);c.center=new Vector3(0,14,0);
        Transform v=Group(t,"Visible_Full_Height_Seal");Block("Translucent_Barrier",v,new Vector3(0,14,0),new Vector3(.11f,28,2.3f),Transparent());
        Material edge=Mat("Seal_Edge",new Color(.23f,.73f,.69f),.55f);for(float h=.25f;h<28;h+=.7f)Block("Broken_Vertical_Rune",v,new Vector3(0,h,-1.2f),new Vector3(.14f,.22f,.12f),edge);
        Block("Seal_Pedestal",p,new Vector3(x,y+.16f,2.3f),new Vector3(.7f,.32f,.7f),dark);Block("Seal_Gem",p,new Vector3(x,y+.42f,2.3f),new Vector3(.25f,.25f,.25f),gold);
        t.gameObject.AddComponent<StageEncounterBarrier>().Configure(g,v.gameObject);
    }
    static void Cache(Transform p,string name,Vector3 pos,string[] types)
    {
        Transform t=Group(p,name);t.localPosition=pos;BoxCollider c=t.gameObject.AddComponent<BoxCollider>();c.isTrigger=true;c.center=new Vector3(0,.6f,0);c.size=new Vector3(1.2f,1.3f,1.6f);
        GameObject[] v=types.Select(a=>Art("Props/Potion_"+(a=="Heal"?"Health":a),t,new Vector3(0,.1f,0),"Ochre")).ToArray();foreach(GameObject o in v)o.transform.localScale*=1.8f;for(int i=0;i<v.Length;i++)v[i].SetActive(i==0);
        t.gameObject.AddComponent<StageSupplyCache>().Configure(types.Select(a=>AssetDatabase.LoadAssetAtPath<ItemData>(Samples+"Item_"+a+".asset")).ToArray(),v);Block("Supply_Inlay",t,new Vector3(0,.015f,0),new Vector3(1,.03f,1),gold);
    }
    static MapData BuildMap()
    {
        MapData m=AssetDatabase.LoadAssetAtPath<MapData>(MapPath);if(m==null){m=ScriptableObject.CreateInstance<MapData>();AssetDatabase.CreateAsset(m,MapPath);}m.id="stage01_city";m.displayName="서울 균열 지구";m.rooms.Clear();m.links.Clear();
        string[] ids={"entry","market","ascent","crossing","cache_east","lockdown","descent","boss","cache_west"},labels={"진입","시장 균열","상행","카페 옥상","설비 연결교","하늘 정원","하행","보스 광장","전망대 보급"};
        Vector2Int[] pos={new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(3,1),new Vector2Int(4,1),new Vector2Int(6,2),new Vector2Int(7,2),new Vector2Int(9,1),new Vector2Int(10,0),new Vector2Int(3,2)};
        for(int i=0;i<ids.Length;i++)m.rooms.Add(new MapRoom{id=ids[i],displayName=labels[i],position=pos[i],size=new Vector2Int(i==1||i==3||i==5||i==7?2:1,1),icon=i==0?MapRoomIcon.Start:i==7?MapRoomIcon.Boss:i==1||i==3||i==5?MapRoomIcon.Gate:i==4||i==8?MapRoomIcon.Item:MapRoomIcon.None,revealedAtStart=i==0});
        for(int i=0;i<7;i++)m.links.Add(new MapLink{fromRoom=ids[i],toRoom=ids[i+1]});m.links.Add(new MapLink{fromRoom="ascent",toRoom="cache_west"});EditorUtility.SetDirty(m);return m;
    }
    static void Room(Transform p,string id,string title,string subtitle,float l,float r,float b,float top)
    {Transform t=Group(p,id+"_"+b);t.localPosition=new Vector3((l+r)*.5f,(b+top)*.5f,0);BoxCollider c=t.gameObject.AddComponent<BoxCollider>();c.isTrigger=true;c.size=new Vector3(r-l,top-b,5);t.gameObject.AddComponent<StageRoomTrigger>().Configure(id,title,subtitle);}
    static Transform Group(Transform p,string name){Transform t=new GameObject(name).transform;t.SetParent(p,false);return t;}
    static GameObject Instance(string path,Transform p,Vector3 pos){GameObject a=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(a==null)throw new InvalidOperationException("Missing asset "+path);GameObject o=(GameObject)PrefabUtility.InstantiatePrefab(a,p);o.transform.localPosition=pos;o.transform.localRotation=Quaternion.identity;return o;}
    static GameObject Art(string path,Transform p,Vector3 pos,string theme="Clay",bool outline=true){GameObject o=Instance(Kit+path+".prefab",p,pos);foreach(Collider c in o.GetComponentsInChildren<Collider>(true))c.enabled=false;Stage01ArtFactory.ApplyLegacyPalette(o,theme,outline);return o;}
    static BoxCollider Box(string name,Transform p,Vector3 pos,Vector3 size,int layer){Transform t=Group(p,name);t.localPosition=pos;t.gameObject.layer=layer;BoxCollider c=t.gameObject.AddComponent<BoxCollider>();c.size=size;return c;}
    static GameObject Block(string name,Transform p,Vector3 pos,Vector3 size,Material mat){GameObject o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(p,false);o.transform.localPosition=pos;o.transform.localScale=size;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=mat;return o;}
    static Material Mat(string name,Color color,float emission=0){string path=Mats+"Level_"+name+".mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);Shader s=Shader.Find("ProjectM/Stage01/City Cel");if(s==null)throw new InvalidOperationException("City Cel shader missing");if(m==null){m=new Material(s);AssetDatabase.CreateAsset(m,path);}m.shader=s;m.SetColor("_BaseColor",color);m.SetFloat("_UseVertexColor",0);m.SetFloat("_Emission",emission);m.SetFloat("_ShadowFloor",.74f);m.SetColor("_ShadowColor",new Color(.57f,.66f,.76f));EditorUtility.SetDirty(m);return m;}
    static Material Transparent(){string path=Mats+"Level_RiftSeal.mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",new Color(.26f,.83f,.78f,.19f));m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;EditorUtility.SetDirty(m);return m;}
    static void Set(UnityEngine.Object obj,string field,Action<SerializedProperty> edit){SerializedObject s=new SerializedObject(obj);SerializedProperty p=s.FindProperty(field);if(p==null)throw new InvalidOperationException(obj.GetType().Name+"."+field);edit(p);s.ApplyModifiedPropertiesWithoutUndo();}
}
