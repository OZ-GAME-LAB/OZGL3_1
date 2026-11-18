Shader "ProjectM/Stage01/Sky Atmosphere"
{
    Properties
    {
        _HorizonColor ("Mint horizon", Color) = (0.70,0.84,0.81,1)
        _ZenithColor ("Blue upper sky", Color) = (0.34,0.57,0.74,1)
        _HorizonY ("Horizon world height", Float) = -12
        _ZenithY ("Upper sky world height", Float) = 18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "SkyAtmosphere"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                float _HorizonY;
                float _ZenithY;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float worldY:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(positionWS);
                output.worldY=positionWS.y;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // World height keeps the atmosphere stable while the camera climbs rooftops.
                float height=saturate((input.worldY-_HorizonY)/max(0.001,_ZenithY-_HorizonY));
                height=height*height*(3.0-2.0*height);
                return half4(lerp(_HorizonColor.rgb,_ZenithColor.rgb,height),1);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
