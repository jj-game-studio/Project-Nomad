Shader "Nomad/Breath Jet"
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
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 Frag(V v):SV_Target
            {
                float2 uv=v.uv;
                float stream=uv.y*24-_Time.y*16;
                float tongues=sin(uv.x*73+sin(stream)*1.8+stream)*0.5+0.5;
                float ribbons=pow(saturate(sin(uv.x*38+sin(stream*0.7)*1.1)),3);
                float side=saturate(1-abs(uv.x*2-1));
                float edge=smoothstep(0,0.13,side)*smoothstep(1,0.8,uv.y);
                float hot=saturate(pow(side,4)*0.6+(1-uv.y)*0.25+ribbons*0.2);
                half3 flame=lerp(half3(1.4,0.12,0.003),half3(2.1,1.4,0.3),hot);
                float alpha=edge*(0.10+0.18*tongues+0.10*ribbons);
                return half4(flame,alpha);
            }
            ENDHLSL
        }
    }
}
