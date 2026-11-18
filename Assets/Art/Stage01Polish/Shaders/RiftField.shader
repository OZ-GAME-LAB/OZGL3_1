Shader "ProjectM/Stage01/Rift Field"
{
    Properties
    {
        _BaseColor("Core",Color)=(0.015,0.035,0.07,1)
        [HDR]_EdgeColor("Rift energy",Color)=(0.12,1.4,1.7,1)
        [HDR]_AccentColor("Arc highlight",Color)=(0.95,2.0,1.7,1)
        _Speed("Flow speed",Range(0,3))=0.55
        _Intensity("Intensity",Range(0,2))=0.9
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+3" }
        Pass
        {
            Name "Rift"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor,_EdgeColor,_AccentColor; half _Speed,_Intensity; CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output; }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2.0-1.0;
                float radius=length(p); clip(1.0-radius);
                float angle=atan2(p.y,p.x);
                float time=_Time.y*_Speed;
                float orbit=angle*3.0+radius*17.0-time*1.7;
                float strand=pow(saturate(1.0-abs(sin(orbit+sin(radius*9.0-time)*0.5))),19.0);
                strand*=smoothstep(0.15,0.72,radius);
                float rim=exp(-abs(radius-0.93)*48.0);
                float innerRim=exp(-abs(radius-0.77)*65.0)*0.2;
                float arc=pow(saturate(sin(angle*7.0-radius*3.0+time)),12.0)*rim;
                float center=pow(saturate(1.0-radius),2.0);
                half3 color=_BaseColor.rgb*(0.45+radius*0.75);
                color+=_EdgeColor.rgb*(rim*0.7+innerRim+strand*0.18)*_Intensity;
                color+=_AccentColor.rgb*arc*0.75*_Intensity;
                color+=_EdgeColor.rgb*center*0.025;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
