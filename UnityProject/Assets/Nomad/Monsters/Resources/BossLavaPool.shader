Shader "Nomad/Lava Pool"
{
    Properties { _Radius("Radius", Float) = 3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float3 _Center;
            float _Radius;
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            V Vert(A v) { V o; o.world = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world); return o; }
            half4 Frag(V v):SV_Target
            {
                float2 uv = (v.world.xz - _Center.xz) / _Radius;
                float radius = length(uv);
                float waves = sin(uv.x * 19 + sin(uv.y * 13 + _Time.y) * 2) * sin(uv.y * 17 - _Time.y * 1.4);
                float crust = smoothstep(0.12, 0.5, waves);
                float edge = smoothstep(0.75, 0.98, radius);
                float bubbles = pow(saturate(sin(uv.x * 31 + _Time.y * 2) * cos(uv.y * 27 - _Time.y)), 18);
                half3 molten = lerp(half3(1.8,0.19,0.006), half3(0.1,0.025,0.02), crust);
                molten += (edge * 0.7 + bubbles * 1.5) * half3(1.6,0.45,0.01);
                return half4(molten, (1 - smoothstep(0.94,1,radius)) * 0.96);
            }
            ENDHLSL
        }
    }
}
