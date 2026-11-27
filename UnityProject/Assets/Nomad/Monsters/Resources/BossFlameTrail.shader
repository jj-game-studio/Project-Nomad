Shader "Nomad/Soft Comet Flame"
{
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
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            float Fbm(float2 p){return Noise(p)*0.55+Noise(p*2.1+8)*0.28+Noise(p*4.3)*0.17;}
            V Vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;}
            half4 Frag(V v):SV_Target
            {
                float along=v.uv.x, across=abs(v.uv.y*2-1);
                float cloud=Fbm(float2(along*7-_Time.y*5,v.uv.y*4));
                float tongue=saturate(1-across+cloud*0.45-0.22);
                float core=pow(saturate(1-across*2.2),3);
                half3 flame=lerp(half3(1.25,0.065,0.004),half3(2.4,0.7,0.035),cloud)+core*half3(2.5,1.8,0.45);
                flame=lerp(flame,half3(0.25,1.3,2.8)+core*0.6,step(v.color.r,v.color.b));
                return half4(flame,tongue*cloud*v.color.a*0.9);
            }
            ENDHLSL
        }
    }
}
