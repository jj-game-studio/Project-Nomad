Shader "Nomad/Boss Outline"
{
    Properties
    {
        _OutlineColor("Color", Color) = (0.03,0.02,0.02,1)
        _Width("World width", Float) = 0.04
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1" "RenderType"="Opaque" }
        Pass
        {
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float _Width;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            float4 Vert(Attributes input):SV_POSITION
            {
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                world += TransformObjectToWorldNormal(input.normalOS) * _Width;
                return TransformWorldToHClip(world);
            }
            half4 Frag():SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
