Shader "Nomad/Volumetric Flame"
{
    Properties { _Opacity("Opacity", Float)=0.45 }
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
            float _Opacity;
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A v)
            {
                V o; float3 pos=v.positionOS.xyz;
                pos.xy*=1+sin(v.uv.y*25+v.uv.x*19-_Time.y*16)*0.10;
                o.positionCS=TransformObjectToHClip(pos); o.uv=v.uv; return o;
            }
            half4 Frag(V v):SV_Target
            {
                float along=v.uv.y, across=v.uv.x;
                float tongues=sin(across*44+sin(along*23-_Time.y*11)*2+along*41-_Time.y*18)*0.5+0.5;
                float veins=pow(saturate(sin(across*68+along*23-_Time.y*14)),5);
                float hot=saturate((1-along)*0.3+tongues*0.35+veins*0.18);
                half3 flame=lerp(half3(0.8,0.025,0.001),half3(2.0,0.65,0.025),hot);
                float fade=1-smoothstep(0.65,1,along);
                return half4(flame,fade*_Opacity*(0.2+0.6*tongues+veins*0.2));
            }
            ENDHLSL
        }
    }
}
