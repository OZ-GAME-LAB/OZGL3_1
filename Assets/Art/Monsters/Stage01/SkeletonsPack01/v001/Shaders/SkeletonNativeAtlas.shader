Shader "ProjectM/Skeleton Native Atlas"
{
    Properties
    {
        _MainTex ("Original skeleton atlas", 2D) = "white" {}
        _Color ("Original base color", Color) = (1,1,1,1)
        _EmissionColor ("Original emission", Color) = (0,0,0,1)
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_ST;
    fixed4 _Color, _EmissionColor;
    struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
    struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; };
    v2f vert(appdata v) {v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.normal=UnityObjectToWorldNormal(v.normal);return o;}
    fixed4 frag(v2f i):SV_Target {
        float light=.65+.35*saturate(dot(normalize(i.normal),normalize(float3(-.3,.8,-.6))));
        fixed4 color=tex2D(_MainTex,i.uv)*_Color;
        color.rgb=color.rgb*light+_EmissionColor.rgb;
        return color;
    }
    ENDCG
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Off
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        Pass {
            Cull Off
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
