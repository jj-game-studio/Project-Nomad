Shader "Nomad/Armor Shell"
{
    Properties { _LayerColor("Layer Color",Color)=(0.17,0.23,0.27,1) _Stress("Stress",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _LayerColor; float _Stress;
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION;float3 pos:TEXCOORD0;float3 normal:TEXCOORD1; };
            V Vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.pos=v.positionOS.xyz;o.normal=TransformObjectToWorldNormal(v.normalOS);return o;}
            half4 Frag(V v):SV_Target
            {
                float light=dot(normalize(v.normal),normalize(float3(-0.4,0.8,-0.2)));
                float shade=light>0.5?1:light>0?0.7:0.36;
                float seams=abs(sin(v.pos.x*16+sin(v.pos.z*13))*cos(v.pos.y*21+v.pos.z*9));
                float crack=(1-smoothstep(0.01,0.035,seams))*smoothstep(0.12,0.8,_Stress);
                return half4(_LayerColor.rgb*shade+crack*half3(1.7,0.35,0.02),1);
            }
            ENDHLSL
        }
    }
}
