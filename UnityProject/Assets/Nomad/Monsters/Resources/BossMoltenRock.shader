Shader "Nomad/Molten Rock"
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
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 local:TEXCOORD0; float3 normal:TEXCOORD1; };
            V Vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.local=v.positionOS.xyz;o.normal=TransformObjectToWorldNormal(v.normalOS);return o; }
            half4 Frag(V v):SV_Target
            {
                float3 pos=v.local*8;
                float seams=abs(sin(pos.x+sin(pos.z*1.7))*sin(pos.y*1.3+cos(pos.x)));
                float crack=1-smoothstep(0.025,0.13,seams);
                float hot=0.75+0.25*sin(_Time.y*5+pos.x*2+pos.z);
                float shade=0.45+0.55*saturate(dot(normalize(v.normal),normalize(float3(-0.4,0.8,-0.3))));
                half3 color=half3(0.075,0.035,0.023)*shade+crack*hot*half3(3.2,0.8,0.035);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
