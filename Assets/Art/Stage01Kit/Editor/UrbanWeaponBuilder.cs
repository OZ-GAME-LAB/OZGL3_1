using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class UrbanWeaponBuilder
{
    private const string Root = "Assets/Art/Stage01Kit";
    private const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";

    public static void Build()
    {
        System.IO.Directory.CreateDirectory(Root + "/Meshes/Weapons");
        System.IO.Directory.CreateDirectory(Root + "/Prefabs/Weapons");
        AssetDatabase.Refresh();
        BuildOne("Sword",0.95f,true,KitWeaponKind.Sword,"SwordSlash");
        BuildOne("Staff",1.4f,false,KitWeaponKind.Staff,"MagicCast");
        foreach (string path in new[] { Hero+"/Protagonist01_Player.prefab", Hero+"/Protagonist01_Visual.prefab" })
        {
            GameObject character=PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform socket=character.GetComponentsInChildren<Transform>(true).First(t=>t.name=="WeaponSocket");
                var equipment=character.GetComponent<HandSocketEquipment>()??character.AddComponent<HandSocketEquipment>();
                AnimationClip punch=AssetDatabase.LoadAllAssetsAtPath(Hero+"/Protagonist01_Actions.fbx").OfType<AnimationClip>().First(c=>c.name=="Punch");
                equipment.Configure(socket,punch);
                PrefabUtility.SaveAsPrefabAsset(character,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(character); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void BuildOne(string name,float targetHeight,bool invert,KitWeaponKind kind,string attackName)
    {
        string source=Root+"/Models/"+name+".fbx";
        var importer=AssetImporter.GetAtPath(source) as ModelImporter;
        if(importer==null) throw new InvalidOperationException("Missing weapon model "+source);
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importAnimation=false;
        importer.SaveAndReimport();
        GameObject sourceObject=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        var allVertices=new List<Vector3>(); var allTriangles=new List<int>();
        try
        {
            sourceObject.transform.position=Vector3.zero;
            foreach(MeshFilter filter in sourceObject.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh=filter.sharedMesh;
                int offset=allVertices.Count;
                allVertices.AddRange(mesh.vertices.Select(filter.transform.TransformPoint));
                allTriangles.AddRange(mesh.triangles.Select(i=>i+offset));
            }
        }
        finally{Object.DestroyImmediate(sourceObject);}
        if(allVertices.Count==0) throw new InvalidOperationException("Weapon has no geometry: "+name);
        Bounds bounds=new Bounds(allVertices[0],Vector3.zero);
        foreach(Vector3 point in allVertices) bounds.Encapsulate(point);
        Quaternion rotation=Quaternion.identity;
        if(bounds.size.x>bounds.size.y && bounds.size.x>bounds.size.z) rotation=Quaternion.FromToRotation(Vector3.right,Vector3.up);
        else if(bounds.size.z>bounds.size.y) rotation=Quaternion.FromToRotation(Vector3.forward,Vector3.up);
        if(invert) rotation=Quaternion.Euler(0,0,180)*rotation;
        allVertices=allVertices.Select(p=>rotation*(p-bounds.center)).ToList();
        bounds=new Bounds(allVertices[0],Vector3.zero); foreach(Vector3 p in allVertices)bounds.Encapsulate(p);
        float factor=targetHeight/bounds.size.y;
        Vector3 baseCenter=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        allVertices=allVertices.Select(p=>(p-baseCenter)*factor).ToList();
        float gripHeight=targetHeight*(kind==KitWeaponKind.Sword?0.14f:0.33f);
        var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var colors=new List<Color>();
        var triangles=new List<int>();
        float finalTip=targetHeight;
        var sourceFaces=new List<Vector3[]>();
        for(int index=0;index<allTriangles.Count;index+=3)
            sourceFaces.Add(new[]{allVertices[allTriangles[index]],allVertices[allTriangles[index+1]],allVertices[allTriangles[index+2]]});
        var coloredFaces=new List<(Vector3[] points, bool crystal)>();
        if(kind==KitWeaponKind.Staff)
        {
            const float crystalScale=.85f, gap=.045f;
            float cut=targetHeight*.735f;
            var body=new List<Vector3[]>();var crystal=new List<Vector3[]>();var rim=new List<Vector3>();
            foreach(var face in sourceFaces)
            {
                Fan(Clip(face,cut,false),body);Fan(Clip(face,cut,true),crystal);
                for(int edge=0;edge<3;edge++)
                {
                    Vector3 a=face[edge],b=face[(edge+1)%3];
                    if((a.y-cut)*(b.y-cut)<0)
                    {
                        Vector3 p=Vector3.Lerp(a,b,(cut-a.y)/(b.y-a.y));
                        if(!rim.Any(q=>(p-q).sqrMagnitude<.00000001f))rim.Add(p);
                    }
                }
            }
            if(crystal.Count==0||rim.Count<3)throw new InvalidOperationException("Cannot separate staff crystal");
            Vector3 center=Vector3.zero;foreach(Vector3 p in rim)center+=p;center/=rim.Count;
            rim=rim.OrderBy(p=>Mathf.Atan2(p.z-center.z,p.x-center.x)).ToList();
            for(int i=0;i<rim.Count;i++)
            {
                Vector3 a=rim[i],b=rim[(i+1)%rim.Count];
                body.Add(new[]{center,b,a});crystal.Add(new[]{center,a,b});
            }
            Bounds headBounds=new Bounds(crystal[0][0],Vector3.zero);
            foreach(var face in crystal)foreach(var p in face)headBounds.Encapsulate(p);
            float shiftedMinimum=headBounds.center.y+(headBounds.min.y-headBounds.center.y)*crystalScale;
            float lift=cut+gap-shiftedMinimum;
            foreach(var face in body)coloredFaces.Add((face,false));
            foreach(var face in crystal)
                coloredFaces.Add((face.Select(p=>headBounds.center+(p-headBounds.center)*crystalScale+Vector3.up*lift).ToArray(),true));
            finalTip=headBounds.center.y+(headBounds.max.y-headBounds.center.y)*crystalScale+lift;
            System.IO.File.WriteAllText("ArtSources/Stage01Kit/staff-refinement.json",JsonUtility.ToJson(new StaffReport
            {crystalScale=crystalScale,clearanceMeters=gap,shaftTop=cut,crystalBottom=cut+gap,tipHeight=finalTip,triangles=coloredFaces.Count},true));
        }
        else foreach(var face in sourceFaces)coloredFaces.Add((face,false));
        for(int index=0;index<coloredFaces.Count;index++)
        {
            var face=coloredFaces[index];Vector3 a=face.points[0],b=face.points[1],c=face.points[2];
            Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            float y=((a+b+c)/3).y/targetHeight;
            Color navy=new Color(.13f,.19f,.25f), silver=new Color(.73f,.81f,.86f), teal=new Color(.23f,.69f,.73f);
            Color color;
            if(kind==KitWeaponKind.Sword) color=y>.31f?silver:y>.265f?teal:y<.055f?silver:navy;
            else color=face.crystal?teal:y>.69f?silver:(y<.04f || y>.13f&&y<.17f || y>.34f&&y<.38f)?silver:navy;
            color*=1f+(index%5-2)*.018f; color.a=face.crystal?.70f:1f;
            foreach(Vector3 point in new[]{a,b,c}){triangles.Add(vertices.Count);vertices.Add(point-Vector3.up*gripHeight);normals.Add(normal);colors.Add(color);}
        }
        var generated=new Mesh{name=name+"_LowPoly_GripPivot"};
        generated.SetVertices(vertices); generated.SetNormals(normals); generated.SetColors(colors);generated.SetTriangles(triangles,0);generated.RecalculateBounds();
        string meshPath=Root+"/Meshes/Weapons/"+name+".asset";
        Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(saved==null){AssetDatabase.CreateAsset(generated,meshPath);saved=generated;}
        else{EditorUtility.CopySerialized(generated,saved);Object.DestroyImmediate(generated);}
        var root=new GameObject(name);
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh=saved;
            root.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Urban_SharedPalette.mat");
            var weapon=root.AddComponent<SocketWeapon>();
            RuntimeAnimatorController controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Hero+"/Protagonist01_"+name+".overrideController");
            AnimationClip clip=AssetDatabase.LoadAllAssetsAtPath(Hero+"/Protagonist01_ExtendedActions.fbx").OfType<AnimationClip>().First(x=>x.name==attackName);
            if(controller==null)throw new InvalidOperationException("Missing weapon animation override: "+name);
            weapon.Configure(kind,new Vector3(0,0,90),controller,clip,kind==KitWeaponKind.Sword?.78f:.73f);
            var tip=new GameObject("EffectTip").transform;tip.SetParent(root.transform,false);tip.localPosition=Vector3.up*(finalTip-gripHeight);
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Weapons/"+name+".prefab");
        }
        finally{Object.DestroyImmediate(root);}
    }

    [Serializable]private class StaffReport{public float crystalScale,clearanceMeters,shaftTop,crystalBottom,tipHeight;public int triangles;}
    private static List<Vector3> Clip(Vector3[] face,float y,bool above)
    {
        var result=new List<Vector3>();
        for(int i=0;i<face.Length;i++)
        {
            Vector3 a=face[i],b=face[(i+1)%face.Length];
            bool inside=above?a.y>=y:a.y<=y, next=above?b.y>=y:b.y<=y;
            if(inside)result.Add(a);
            if(inside!=next)result.Add(Vector3.Lerp(a,b,(y-a.y)/(b.y-a.y)));
        }
        return result;
    }
    private static void Fan(List<Vector3> polygon,List<Vector3[]> result)
    {
        for(int i=1;i+1<polygon.Count;i++)
            if(Vector3.Cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]).sqrMagnitude>.0000000001f)
                result.Add(new[]{polygon[0],polygon[i],polygon[i+1]});
    }
}
