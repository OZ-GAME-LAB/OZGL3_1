Shader "ProjectM/Stage01/City Cel"
{
    Properties
    {
        _BaseColor ("Base color", Color) = (1,1,1,1)
        _UseVertexColor ("Use painted vertex palette", Range(0,1)) = 0
        _ShadowColor ("Cool shadow tint", Color) = (0.62,0.70,0.83,1)
        _ShadowFloor ("Shadow value floor", Range(0,1)) = 0.80
        _LightThreshold ("Light band threshold", Range(0,1)) = 0.48
        _BandSoftness ("Band edge softness", Range(0.001,0.2)) = 0.035
        _Emission ("Emission", Range(0,4)) = 0
        _DistanceFade ("Atmospheric distance fade", Range(0,1)) = 0
        _HazeColor ("Atmospheric color", Color) = (0.44,0.58,0.64,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        // All passes intentionally share this layout. Foreign UsePass layouts can break SRP batching.
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShadowColor, _HazeColor;
            half _UseVertexColor, _ShadowFloor, _LightThreshold, _BandSoftness;
            half _Emission, _DistanceFade;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=position.positionCS; output.positionWS=position.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color; output.fog=ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal=normalize(input.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normal,light.direction)*0.65h+0.35h);
                half lit=smoothstep(_LightThreshold-_BandSoftness,_LightThreshold+_BandSoftness,diffuse);
                lit*=lerp(0.20h,1.0h,light.shadowAttenuation);
                half3 paint=lerp(half3(1,1,1),input.color.rgb,_UseVertexColor)*_BaseColor.rgb;
                half3 shaded=paint*lerp(_ShadowColor.rgb*_ShadowFloor,half3(1.04h,0.98h,0.87h),lit);
                // A restrained hemisphere fill preserves shapes without bleaching painted colors.
                shaded+=paint*half3(0.10h,0.12h,0.14h);
                shaded+=paint*_Emission;
                shaded=lerp(shaded,_HazeColor.rgb,_DistanceFade);
                return half4(MixFog(shaded,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            struct ShadowAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShadowVaryings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output=(ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS=TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS=TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction=normalize(_LightPosition-positionWS);
                #else
                    float3 direction=_LightDirection;
                #endif
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z=min(output.positionCS.z,output.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z=max(output.positionCS.z,output.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            half4 ShadowFrag(ShadowVaryings input):SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct DepthAttributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output=(DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 DepthFrag(DepthVaryings input):SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex NormalVert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct NormalAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct NormalVaryings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            NormalVaryings NormalVert(NormalAttributes input)
            {
                NormalVaryings output=(NormalVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 NormalFrag(NormalVaryings input):SV_Target
            {
                float3 normal=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
