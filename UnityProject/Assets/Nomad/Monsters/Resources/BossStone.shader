Shader "Nomad/Volcanic Stone"
{
    Properties { [MainColor] _BaseColor("Stone tint", Color) = (0.3,0.27,0.3,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float4 _BaseColor;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            V Vert(A v)
            {
                V o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            float Hash(float2 v) { return frac(sin(dot(v,float2(127.1,311.7))) * 43758.5453); }
            half4 Frag(V v):SV_Target
            {
                Light sun = GetMainLight(TransformWorldToShadowCoord(v.world));
                half diffuse = saturate(dot(normalize(v.normal),sun.direction)) * sun.shadowAttenuation;
                half band = diffuse > 0.65 ? 1.15 : diffuse > 0.2 ? 0.88 : 0.56;
                float2 uv = v.world.xz + v.world.y * float2(0.37,0.61);
                float grain = Hash(floor(uv * 24));
                float patch = Hash(floor(uv * 1.8));
                float fissure = 1 - smoothstep(0.01,0.04,abs(sin(uv.x * 0.9 + uv.y * 1.4 + sin(uv.y * 2) * 0.6)));
                half3 stone = _BaseColor.rgb * (0.88 + grain * 0.17 + patch * 0.16) * (1 - fissure * 0.18);
                return half4(stone * band + half3(0.018,0.01,0.007) * saturate(v.normal.y),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
