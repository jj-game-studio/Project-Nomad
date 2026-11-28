Shader "Nomad/Swirling Fire Annulus"
{
    Properties { _MainTex("Reference flame",2D)="white"{} _Inner("Inner",Float)=0 _Age("Age",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            float _Inner,_Age;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 Frag(V v):SV_Target
            {
                float2 p=v.uv*2-1;float radius=length(p);float spin=_Age*0.25;
                float2 rotated=float2(p.x*cos(spin)-p.y*sin(spin),p.x*sin(spin)+p.y*cos(spin));
                half4 texColor=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,rotated*0.5+0.5);
                float edge=exp(-abs(radius-0.97)*55)+(_Inner>0.01?exp(-abs(radius-_Inner-0.02)*55):0);
                float wave=sin((radius-_Inner)/max(0.01,1-_Inner)*12-_Age*13)*0.5+0.5;
                float fade=1-smoothstep(0.65,1,_Age);
                half3 color=texColor.rgb*1.25+edge*half3(1.8,0.45,0.02);
                return half4(color,fade*saturate(texColor.a*0.7+edge*0.45)*(0.65+wave*0.35));
            }
            ENDHLSL
        }
    }
}
