Shader "Nomad/Boss Cel"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _SignalColor("Signal", Color) = (0,0,0,0)
        _Enraged("Burning eyes", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _SignalColor;
            float3 _EyeLeft, _EyeRight;
            float _Enraged;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float3 positionWS:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalize(input.normalWS), sun.direction)) * sun.shadowAttenuation;
                half band = diffuse > 0.65 ? 1.15 : diffuse > 0.2 ? 0.82 : 0.48;
                half3 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 albedo = texColor * _BaseColor.rgb;
                albedo = lerp(albedo, albedo * half3(1.65,0.36,0.24), _Enraged * 0.75);
                // 원본 눈의 밝은 황금색만 붉게 발광시켜 눈 표면에서도 광폭을 구분합니다.
                half eyeArea = 1 - smoothstep(0.22, 0.62, min(distance(input.positionWS, _EyeLeft), distance(input.positionWS, _EyeRight)));
                half eyes = eyeArea * saturate(texColor.r * 1.5 - texColor.b * 0.8);
                half fissure = pow(saturate(sin(input.uv.x * 117 + sin(input.uv.y * 83) * 3)), 24);
                half heat = (0.65 + 0.35 * sin(_Time.y * 5 + input.positionWS.y * 3));
                half rim = pow(1 - abs(dot(normalize(input.normalWS), normalize(_WorldSpaceCameraPos - input.positionWS))), 2);
                return half4(albedo * band + _SignalColor.rgb * (0.18 + rim * 0.65) + _Enraged * (eyes * half3(4,0.035,0) + fissure * heat * half3(0.65,0.07,0.005) + rim * half3(0.25,0.015,0)), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
