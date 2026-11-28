Shader "Nomad/Carved Ground Fissure"
{
    Properties { _Fade("Fade",Float)=1 _Heat("Fresh edge heat",Float)=1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Fade,_Heat;
            struct A{float4 position:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            struct V{float4 position:SV_POSITION;half4 color:COLOR;float heat:TEXCOORD0;};
            V Vert(A v){V o;o.position=TransformObjectToHClip(v.position.xyz);o.color=v.color;o.heat=v.uv.x;return o;}
            half4 Frag(V v):SV_Target{return half4(v.color.rgb+v.heat*_Heat*half3(1.8,0.3,0.015),v.color.a*_Fade);}
            ENDHLSL
        }
    }
}
