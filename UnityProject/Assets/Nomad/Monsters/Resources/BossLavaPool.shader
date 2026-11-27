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
            float _Radius, _ShapeSeed;
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            V Vert(A v) { V o; o.world = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world); return o; }
            half4 Frag(V v):SV_Target
            {
                float2 uv = (v.world.xz - _Center.xz) / _Radius;
                float radians = atan2(uv.x, uv.y);
                float boundary = 0.82 + 0.1 * sin(radians * 3 + _ShapeSeed) + 0.06 * cos(radians * 5 - _ShapeSeed * 0.7);
                float radius = length(uv) / boundary;
                float2 flow=uv*3.6+float2(sin(uv.y*4+_Time.y*0.45),cos(uv.x*5-_Time.y*0.35))*0.17;
                float2 cell=floor(flow), fractal=frac(flow);
                float nearest=8, second=8;
                for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
                {
                    float2 grid=float2(x,y);
                    float2 seed=cell+grid;
                    float2 jitter=frac(sin(float2(dot(seed,float2(127.1,311.7)),dot(seed,float2(269.5,183.3))))*43758.5453);
                    float distance=length(grid+jitter-fractal);
                    if(distance<nearest){second=nearest;nearest=distance;}else second=min(second,distance);
                }
                float seam=1-smoothstep(0.08,0.26,second-nearest);
                float edge=exp(-abs(radius-0.92)*32);
                float bubbles=pow(saturate(sin(uv.x*41+_Time.y*2)*cos(uv.y*37-_Time.y)),24);
                float pulse=0.85+0.15*sin(_Time.y*2+uv.x*5+uv.y*7);
                half3 molten=lerp(half3(0.095,0.032,0.018),half3(2.6,0.48,0.008)*pulse,seam);
                molten+=(edge+bubbles)*half3(2.8,0.85,0.03);
                return half4(molten, (1 - smoothstep(0.94,1,radius)) * 0.96);
            }
            ENDHLSL
        }
    }
}
