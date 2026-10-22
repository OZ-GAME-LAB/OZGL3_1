Shader "ProjectM/Staff Orb Faceted Transparent"
{
    Properties
    {
        _BaseColor ("Crystal Tint and Transparency", Color) = (0.20,0.73,0.81,0.40)
        _RimColor ("Crystal Edge Tint", Color) = (0.62,0.94,0.97,1)
        _RimStrength ("Edge Tint Strength", Range(0,1)) = 0.30
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "CrystalSurface"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimStrength;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 facetTint : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.facetTint = input.color.rgb;
                output.fog = ComputeFogFactor(pos.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal = normalize(input.normalWS);
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(dot(normal, view)), 3.0h);
                Light mainLight = GetMainLight();
                half lightFacing = saturate(dot(normal, mainLight.direction));
                half3 ambient = max(SampleSH(normal), half3(.28h,.28h,.28h));
                half3 lighting = ambient + mainLight.color * (.25h + .55h * lightFacing);
                half3 color = _BaseColor.rgb * input.facetTint * lighting;
                color = lerp(color, _RimColor.rgb, rim * _RimStrength);
                // One front surface: background remains visible without a screen texture or extra pass.
                half alpha = saturate(_BaseColor.a + rim * .21h);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
