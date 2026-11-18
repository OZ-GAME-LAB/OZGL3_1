Shader "ProjectM/Stage01/Rift Spark"
{
    Properties { [HDR]_BaseColor("Spark color",Color)=(0.25,1.0,1.2,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input) { Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; output.color=input.color; return output; }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=abs(input.uv*2.0-1.0);
                half a=saturate(1.0-p.x-p.y); a=a*a;
                return half4(_BaseColor.rgb*input.color.rgb,a*input.color.a*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
