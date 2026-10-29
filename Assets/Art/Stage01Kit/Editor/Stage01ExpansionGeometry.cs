#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Authored mesh tools for the additive Stage 1 kit. Existing assets keep their GUIDs and geometry.
public sealed class S1Geo
{
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> triangles = new List<int>();
    public void Box(Vector3 c, Vector3 s, Color color) => BoxRotated(c, s, Quaternion.identity, color);
    public void BoxRotated(Vector3 c, Vector3 s, Quaternion q, Color color)
    {
        Vector3 h=s*.5f;
        var p=new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),
            new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z)};
        for(int i=0;i<p.Length;i++)p[i]=c+q*p[i];
        Quad(p[0],p[3],p[2],p[1],color);Quad(p[4],p[5],p[6],p[7],color);
        Quad(p[0],p[4],p[7],p[3],color);Quad(p[1],p[2],p[6],p[5],color);
        Quad(p[3],p[7],p[6],p[2],color);Quad(p[0],p[1],p[5],p[4],color);
    }
    public void TaperedBox(Vector3 c,Vector3 s,Vector2 top,Color color)
    {
        float y0=c.y-s.y*.5f,y1=c.y+s.y*.5f;
        var a=new Vector3(c.x-s.x*.5f,y0,c.z-s.z*.5f);var b=new Vector3(c.x+s.x*.5f,y0,c.z-s.z*.5f);
        var d=new Vector3(c.x-s.x*.5f,y0,c.z+s.z*.5f);var e=new Vector3(c.x+s.x*.5f,y0,c.z+s.z*.5f);
        var aa=new Vector3(c.x-top.x*.5f,y1,c.z-top.y*.5f);var bb=new Vector3(c.x+top.x*.5f,y1,c.z-top.y*.5f);
        var dd=new Vector3(c.x-top.x*.5f,y1,c.z+top.y*.5f);var ee=new Vector3(c.x+top.x*.5f,y1,c.z+top.y*.5f);
        Quad(a,aa,bb,b,color);Quad(d,e,ee,dd,color);Quad(a,d,dd,aa,color);Quad(b,bb,ee,e,color);
        Quad(aa,dd,ee,bb,color);Quad(a,b,e,d,color);
    }
    public void Beam(Vector3 a,Vector3 b,float width,float depth,Color color)
    {
        var d=b-a;if(d.sqrMagnitude<1e-10f)throw new ArgumentException("Zero-length beam");
        BoxRotated((a+b)*.5f,new Vector3(width,d.magnitude,depth),Quaternion.FromToRotation(Vector3.up,d.normalized),color);
    }
    public void Cylinder(Vector3 c,float r,float h,int sides,Color color)=>Cone(c,r,r,h,sides,color);
    public void Cone(Vector3 c,float rb,float rt,float h,int sides,Color color)
    {
        var bot=c+Vector3.down*h*.5f;var top=c+Vector3.up*h*.5f;
        for(int i=0;i<sides;i++){
            float a=Mathf.PI*2*i/sides,b=Mathf.PI*2*(i+1)/sides;
            var u=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var v=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
            if(rt<1e-6f)Tri(bot+u*rb,top,bot+v*rb,color);
            else if(rb<1e-6f)Tri(bot,top+u*rt,top+v*rt,color);
            else Quad(bot+u*rb,top+u*rt,top+v*rt,bot+v*rb,color);
            if(rt>1e-6f)Tri(top,top+v*rt,top+u*rt,color);
            if(rb>1e-6f)Tri(bot,bot+u*rb,bot+v*rb,color);
        }
    }
    public void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
    {
        int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
        colors.Add(color);colors.Add(color);colors.Add(color);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
    }
    public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    {Tri(a,b,c,color);Tri(a,c,d,color);}
    public Mesh ToMesh(string name)
    {
        var m=new Mesh{name=name};if(vertices.Count>65535)m.indexFormat=IndexFormat.UInt32;
        m.SetVertices(vertices);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
}

public static class S1Assets
{
    public const string Root="Assets/Art/Stage01Kit";
    public const string Prefabs=Root+"/Prefabs/Expansion";
    const string Meshes=Root+"/Meshes/Expansion";
    public static Color Hex(string h){ColorUtility.TryParseHtmlString("#"+h,out var c);return c;}
    public static GameObject Save(string name,S1Geo high,S1Geo low=null)
    {
        Directory.CreateDirectory(Prefabs);Directory.CreateDirectory(Meshes);AssetDatabase.Refresh();
        var root=new GameObject(name);
        var a=Attach(root,high,name+(low==null?"":"_LOD0"));
        if(low!=null){var b=Attach(root,low,name+"_LOD1");var lod=root.AddComponent<LODGroup>();
            lod.SetLODs(new[]{new LOD(.10f,new Renderer[]{a}),new LOD(.025f,new Renderer[]{b})});lod.RecalculateBounds();}
        return root;
    }
    static MeshRenderer Attach(GameObject root,S1Geo geo,string name)
    {
        var mesh=geo.ToMesh(name);string path=Meshes+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);mesh=saved;}
        else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Urban_SharedPalette.mat");
        if(!r.sharedMaterial)throw new FileNotFoundException("Existing Urban_SharedPalette.mat is required.");
        r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.OccluderStatic);
        return r;
    }
    public static void Anchor(GameObject root,string name,Vector3 p)
    {var a=new GameObject(name);a.transform.SetParent(root.transform,false);a.transform.localPosition=p;}
    public static void Collider(GameObject root,string name,Vector3 center,Vector3 size)
    {
        var go=new GameObject("Collision_"+name);go.transform.SetParent(root.transform,false);go.layer=LayerMask.NameToLayer("Ground");
        if(go.layer<0)throw new InvalidOperationException("Ground layer is required.");
        var box=go.AddComponent<BoxCollider>();box.center=center;box.size=size;
    }
    public static void Finish(GameObject root)
    {PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/"+root.name+".prefab");UnityEngine.Object.DestroyImmediate(root);}
}
#endif
