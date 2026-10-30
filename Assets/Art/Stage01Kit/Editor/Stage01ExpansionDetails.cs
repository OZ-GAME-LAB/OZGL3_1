#if UNITY_EDITOR
using UnityEngine;

public static class Stage01ExpansionDetails
{
    static readonly Color Dark=S1Assets.Hex("434E5A"),Light=S1Assets.Hex("747D84"),Navy=S1Assets.Hex("22313E"),
        Ivory=S1Assets.Hex("A5AEAE"),Teal=S1Assets.Hex("486F73"),Metal=S1Assets.Hex("465563"),Ochre=S1Assets.Hex("9B895F");
    public static void Build()
    {Crosswalk();Alley();}
    static void Crosswalk()
    {
        var g=new S1Geo();g.Box(new Vector3(0,-.11f,0),new Vector3(6,.22f,6),S1Assets.Hex("29333F"));
        for(int i=0;i<7;i++)g.Box(new Vector3(-2.55f+i*.85f,.006f,0),new Vector3(.45f,.012f,4.3f),Ivory);
        foreach(int s in new[]{-1,1}){
            g.Box(new Vector3(0,.006f,s*2.65f),new Vector3(6,.012f,.10f),Ochre);
            for(int j=0;j<2;j++){
                var p=new Vector3(s*2.6f,.012f,-1.8f+j*3.6f);g.Box(p,new Vector3(.35f,.024f,.6f),Metal);
                for(int k=0;k<4;k++)g.Box(p+new Vector3(0,.015f,-.21f+k*.14f),new Vector3(.27f,.006f,.035f),Navy);
            }
        }
        var root=S1Assets.Save("Road_Crosswalk_6m",g);S1Assets.Collider(root,"Road",new Vector3(0,-.11f,0),new Vector3(6,.22f,6));
        S1Assets.Anchor(root,"WalkableSurface",Vector3.zero);S1Assets.Anchor(root,"SnapLeft",new Vector3(-3,0,0));S1Assets.Anchor(root,"SnapRight",new Vector3(3,0,0));S1Assets.Finish(root);
    }
    static void Alley()
    {
        var g=new S1Geo();g.Box(new Vector3(0,-.1f,0),new Vector3(6,.2f,2.4f),Dark);
        // Open camera-facing side and open ends expose the return route at Z=0.
        g.Box(new Vector3(0,1.7f,1.25f),new Vector3(6,3.4f,.22f),S1Assets.Hex("5B626B"));
        for(int i=0;i<7;i++){
            float x=-2.7f+i*.9f;g.Box(new Vector3(x,.006f,.15f),new Vector3(.035f,.012f,2.05f),Navy);
            g.Box(new Vector3(x,1.15f,1.11f),new Vector3(.66f,.06f,.035f),Light);
        }
        foreach(int s in new[]{-1,1}){
            g.Box(new Vector3(s*2.88f,1.7f,1.05f),new Vector3(.24f,3.4f,.3f),Light);
            g.Box(new Vector3(s*2.3f,1.6f,1.05f),new Vector3(.1f,3.2f,.12f),Metal);
        }
        g.Box(new Vector3(0,2.95f,1.04f),new Vector3(5.7f,.14f,.18f),Metal);
        g.Box(new Vector3(-1.1f,1.1f,1.08f),new Vector3(.8f,1.4f,.12f),Navy);
        for(int i=0;i<7;i++)g.Box(new Vector3(-1.1f,.6f+i*.16f,.99f),new Vector3(.65f,.06f,.08f),Metal);
        g.Box(new Vector3(1.5f,1.9f,1.06f),new Vector3(1.25f,.5f,.14f),Teal);
        // Geometric direction arrow, without text/brand textures.
        g.Box(new Vector3(1.4f,1.9f,.975f),new Vector3(.65f,.07f,.025f),Ivory);
        g.BoxRotated(new Vector3(1.75f,1.98f,.975f),new Vector3(.3f,.07f,.025f),Quaternion.Euler(0,0,-35),Ivory);
        g.BoxRotated(new Vector3(1.75f,1.82f,.975f),new Vector3(.3f,.07f,.025f),Quaternion.Euler(0,0,35),Ivory);
        var root=S1Assets.Save("Alley_Passage_6m",g);S1Assets.Collider(root,"Floor",new Vector3(0,-.1f,0),new Vector3(6,.2f,2.4f));
        S1Assets.Collider(root,"RearWall",new Vector3(0,1.7f,1.25f),new Vector3(6,3.4f,.22f));
        S1Assets.Anchor(root,"WalkableSurface",Vector3.zero);S1Assets.Anchor(root,"Entry",new Vector3(-3,0,0));S1Assets.Anchor(root,"Exit",new Vector3(3,0,0));
        S1Assets.Anchor(root,"ItemDetour",new Vector3(1.7f,0,.4f));S1Assets.Finish(root);
    }
}
#endif
