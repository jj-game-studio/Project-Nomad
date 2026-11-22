Shader "Nomad/Visibility/Foliage Cutaway"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.1
        _CutHeight("Remaining World Height", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NomadVisibilityDistances); SAMPLER(sampler_NomadVisibilityDistances);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Cutoff;
            float _CutHeight;
        CBUFFER_END
        float4 _NomadVisibilityEye;
        float4 _NomadVisibilityForward;
        float4 _NomadVisibilityParams;
        float _NomadVisibilityActive;

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fog : TEXCOORD3;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 Surface(Varyings input)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            clip(color.a - _Cutoff);
            if (_NomadVisibilityActive > 0.5 && input.positionWS.y > _CutHeight)
            {
                float2 delta = input.positionWS.xz - _NomadVisibilityEye.xz;
                float2 forward = _NomadVisibilityForward.xz;
                float signedAngle = atan2(forward.y * delta.x - forward.x * delta.y, dot(forward, delta));
                float halfAngle = _NomadVisibilityParams.y * 0.5;
                if (abs(signedAngle) <= halfAngle && length(delta) <= _NomadVisibilityParams.x)
                {
                    float u = saturate((signedAngle + halfAngle) / (2.0 * halfAngle));
                    float stepSize = 1.0 / _NomadVisibilityParams.z;
                    // 경계의 양쪽 중 짧은 거리를 사용해 얇은 벽 뒤 누출을 줄입니다.
                    float a = SAMPLE_TEXTURE2D_LOD(_NomadVisibilityDistances, sampler_NomadVisibilityDistances, float2(u, 0.5), 0).r;
                    float b = SAMPLE_TEXTURE2D_LOD(_NomadVisibilityDistances, sampler_NomadVisibilityDistances, float2(saturate(u - stepSize), 0.5), 0).r;
                    float c = SAMPLE_TEXTURE2D_LOD(_NomadVisibilityDistances, sampler_NomadVisibilityDistances, float2(saturate(u + stepSize), 0.5), 0).r;
                    clip(length(delta) - min(a, min(b, c)) + 0.05);
                }
            }
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = Surface(input);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 normal = normalize(input.normalWS);
                half lighting = saturate(dot(normal, light.direction)) * 0.7h + 0.3h;
                color.rgb *= SampleSH(normal) + light.color * lighting * light.shadowAttenuation;
                color.rgb = MixFog(color.rgb, input.fog);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            half4 Depth(Varyings input) : SV_Target { Surface(input); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            half4 Depth(Varyings input) : SV_Target { Surface(input); return 0; }
            ENDHLSL
        }
    }
}

