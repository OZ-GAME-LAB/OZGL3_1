Shader "ProjectM/Preview VFX"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (1,1,1,1)
        _Mask("Mask 0 Mesh / 2 Spark / 3 Smoke", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "VFXPreview"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Mask, _SrcBlend, _DstBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = input.color;
                float2 p = input.uv * 2 - 1;
                if (_Mask > 2.5)
                {
                    float hex = max(abs(p.y), abs(p.x) * .866 + abs(p.y) * .5);
                    float facet = floor((p.x + p.y * .35) * 4) * .08;
                    color.rgb *= saturate(.72 - p.y * .18 + facet);
                    color.a *= 1 - smoothstep(.65, .98, hex);
                }
                else if (_Mask > 1.5)
                    color.a *= saturate(1 - abs(p.x)) * saturate(1 - abs(p.y));
                return color;
            }
            ENDHLSL
        }
    }
}
