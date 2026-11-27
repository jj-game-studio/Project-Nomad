Shader "Nomad/Boss Golden Guard Pulse"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Age;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; float height:TEXCOORD2; };
            V Vert(A a)
            {
                V o;
                o.world = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(a.normalOS);
                o.height = a.positionOS.y;
                return o;
            }
            half4 Frag(V v):SV_Target
            {
                float rim = pow(1 - abs(dot(normalize(v.normal),normalize(_WorldSpaceCameraPos - v.world))),3);
                float bands = pow(saturate(cos(v.height * 23 - _Age * 7)),18);
                float edge = 1 - smoothstep(0.01,0.055,v.height);
                float fade = saturate(1 - _Age);
                return half4(half3(2.2,1.5,0.35), (0.035 + rim * 0.65 + edge * 0.65 + bands * 0.12) * fade);
            }
            ENDHLSL
        }
    }
}
