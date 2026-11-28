Shader "Nomad/Volumetric Flame"
{
    Properties { _Opacity("Opacity", Float)=0.32 _Fan("Continuous fan",Float)=0 }
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
            float _Opacity,_Fan;
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            float Cloud(float2 p){return Noise(p)*0.6+Noise(p*2.1+8)*0.28+Noise(p*4.3)*0.12;}
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A v)
            {
                V o; float3 pos=v.positionOS.xyz;
                if(_Fan<0.5)pos.xy*=1+sin(v.uv.y*25+v.uv.x*19-_Time.y*16)*0.045;
                else pos.y+=sin(v.uv.y*18+v.uv.x*12-_Time.y*10)*0.08;
                o.positionCS=TransformObjectToHClip(pos); o.uv=v.uv; return o;
            }
            half4 Frag(V v):SV_Target
            {
                float along=v.uv.y, across=v.uv.x;
                float angle=across*6.283185;
                float density=Cloud(float2(cos(angle)*3+along*7-_Time.y*4,sin(angle)*3+along*5-_Time.y*3));
                float hot=saturate((1-along)*0.3+density*0.65);
                half3 flame=lerp(half3(1.1,0.035,0.001),half3(2.5,0.65,0.035),hot);
                float fade=1-smoothstep(_Fan>0.5?0.96:0.88,1,along);
                return half4(flame,fade*_Opacity*(0.5+density*0.5));
            }
            ENDHLSL
        }
    }
}
