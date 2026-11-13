Shader "OZGL3/Effects/Blade Ribbon"
{
    Properties
    {
        [MainTexture] _MaskMap("Sweep Detail", 2D) = "white" {}
        [HDR][MainColor] _Tint("Light Tint", Color) = (0.45,0.8,1,1)
        _Intensity("Intensity", Range(0,8)) = 2
        _DetailStrength("Texture Detail", Range(0,1)) = 0.35
        _CoreWidth("Sharp Edge Width", Range(0.01,0.5)) = 0.14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Name "BladeLight"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _MaskMap_ST;
                half4 _Tint;
                half _Intensity;
                half _DetailStrength;
                half _CoreWidth;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half2 uv = saturate(input.uv);
                half4 detail = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, TRANSFORM_TEX(input.uv, _MaskMap));
                half mask = lerp(1.0h, max(detail.r,max(detail.g,detail.b)) * detail.a, _DetailStrength);
                // The leading blade edge stays crisp; only the old sweep is soft.
                half edge = 1.0h - smoothstep(0.0h, _CoreWidth, uv.x);
                half radial = smoothstep(0.38h, 0.88h, uv.y) * (1.0h - smoothstep(0.96h,1.0h,uv.y));
                half3 hue = _Tint.rgb * input.color.rgb;
                half3 light = lerp(hue, half3(1.0h, 0.97h, 0.9h), edge * 0.72h) * _Intensity;
                half opacity = saturate(input.color.a * _Tint.a * radial * mask);
                return half4(light, opacity);
            }
            ENDHLSL
        }
    }
}
