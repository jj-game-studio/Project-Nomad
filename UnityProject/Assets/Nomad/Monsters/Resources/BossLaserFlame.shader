Shader "Nomad/Solid Fire Laser"
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
            struct A{float4 position:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A v){V o;o.position=TransformObjectToHClip(v.position.xyz);o.uv=v.uv;return o;}
            half4 Frag(V v):SV_Target
            {
                float side=abs(v.uv.y*2-1);
                float flow=sin(v.uv.x*54-_Time.y*22+sin(v.uv.y*17))*0.5+0.5;
                float core=pow(saturate(1-side*1.5),2);
                half3 color=half3(2.2,0.18,0.005)+flow*half3(0.5,0.35,0.02)+core*half3(1.5,1.0,0.25);
                return half4(color,pow(saturate(1-side),0.35)*(0.75+flow*0.25));
            }
            ENDHLSL
        }
    }
}
