Shader "ProjectM/Stage01/City Ink Hull"
{
    Properties { _BaseColor("Ink",Color)=(0.035,0.065,0.10,1) _OutlineWidth("World outline width",Range(0,0.15))=0.025 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-1" }
        Pass
        {
            Name "Ink"
            Tags { "LightMode"="UniversalForward" }
            Cull Front ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; float _OutlineWidth; CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half fog:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS=TransformObjectToWorld(input.positionOS.xyz);
                positionWS+=normalize(TransformObjectToWorldNormal(input.normalOS))*_OutlineWidth;
                output.positionCS=TransformWorldToHClip(positionWS);
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target { return half4(MixFog(_BaseColor.rgb,input.fog),1); }
            ENDHLSL
        }
    }
}
