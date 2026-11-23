Shader "Nomad/Visibility/Shade"
{
    // 화면 깊이로 월드 위치를 복원해 시야 밖(부채꼴·주변 원형 밖, 장애물 뒤)을 어둡게 덮습니다.
    Properties
    {
        _ShadeColor("Shade Color", Color) = (0.02, 0.03, 0.06, 0.7)
        _Softness("Edge Softness (m)", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+50" "IgnoreProjector"="True" }
        Pass
        {
            Name "Shade"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_NomadVisibilityDistances); SAMPLER(sampler_NomadVisibilityDistances);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShadeColor;
                float _Softness;
            CBUFFER_END
            float4 _NomadVisibilityEye;
            float4 _NomadVisibilityForward;
            float4 _NomadVisibilityParams;
            float _NomadVisibilityActive;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            // 사각형 메시(-0.5~0.5)를 화면 전체로 펼칩니다.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy * 2.0, UNITY_NEAR_CLIP_VALUE, 1.0);
                return output;
            }

            float RayDistance(float u)
            {
                return SAMPLE_TEXTURE2D_LOD(_NomadVisibilityDistances, sampler_NomadVisibilityDistances, float2(u, 0.5), 0).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(_NomadVisibilityActive - 0.5);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                // 식생 방사선과 같은 각도 규칙(전방 기준 시계 방향)으로 막히기 전 거리를 읽습니다.
                float2 delta = world.xz - _NomadVisibilityEye.xz;
                float2 forward = _NomadVisibilityForward.xz;
                float signedAngle = atan2(forward.y * delta.x - forward.x * delta.y, dot(forward, delta));
                float u = saturate((signedAngle + PI) / TWO_PI);
                float stepSize = 1.0 / _NomadVisibilityParams.z;
                float clear = min(RayDistance(u), min(RayDistance(saturate(u - stepSize)), RayDistance(saturate(u + stepSize))));

                float hidden = smoothstep(clear, clear + max(_Softness, 0.001), length(delta));
                return half4(_ShadeColor.rgb, _ShadeColor.a * hidden);
            }
            ENDHLSL
        }
    }
}
