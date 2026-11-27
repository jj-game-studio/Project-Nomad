Shader "Nomad/Cartoon Lava"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            Varyings Vert(float4 positionOS:POSITION)
            {
                Varyings output;
                output.world = TransformObjectToWorld(positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p = input.world.xz;
                p *= 0.24;
                p += float2(sin(p.y * 1.3 + _Time.y * 0.18), cos(p.x * 1.1 - _Time.y * 0.1)) * 0.4;
                float2 cell = floor(p), f = frac(p);
                float nearest = 10, second = 10;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    float2 neighbor = float2(x,y);
                    float2 key = cell + neighbor;
                    float2 jitter = frac(sin(float2(dot(key,float2(127.1,311.7)),dot(key,float2(269.5,183.3)))) * 43758.5453);
                    float distance = length(neighbor + jitter - f);
                    if (distance < nearest) { second = nearest; nearest = distance; }
                    else second = min(second,distance);
                }
                float seam = second - nearest;
                float flow = sin(input.world.x * 0.6 + input.world.z * 0.4 - _Time.y) * 0.5 + 0.5;
                half3 color = lerp(half3(1.65,0.28,0.012),half3(0.13,0.026,0.025),smoothstep(0.04,0.32,seam));
                color += half3(0.3,0.035,0) * flow * (1 - smoothstep(0.02,0.12,seam));
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
