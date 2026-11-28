Shader "Nomad/Eruption Flame Column"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 pos:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            V Vert(A v){V o;o.pos=TransformObjectToHClip(v.pos.xyz);o.uv=v.uv;o.color=v.color;return o;}
            half4 Frag(V v):SV_Target
            {
                float height=v.uv.x,side=abs(v.uv.y*2-1);
                float tongues=sin(height*28-_Time.y*18+sin(v.uv.y*13))*0.1+sin(height*51-_Time.y*23)*0.06;
                float edge=smoothstep(1,0.25,side+height*0.15+tongues);
                float core=pow(saturate(1-side*2),2)*(1-height*0.6);
                half3 fire=lerp(half3(2.2,0.12,0.003),half3(3,0.7,0.02),edge)+core*half3(2.3,1.7,0.45);
                return half4(fire,edge*saturate((1-height)*2)*v.color.a*0.85);
            }
            ENDHLSL
        }
    }
}
