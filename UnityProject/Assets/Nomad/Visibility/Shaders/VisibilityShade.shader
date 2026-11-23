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

                float2 delta = world.xz - _NomadVisibilityEye.xz;
                float span = length(delta);
                float soft = max(_Softness, 0.001);

                // 방사선은 월드 +Z 기준 시계 방향 각도로 저장되어 있습니다. 이웃 표본을 선형 보간합니다.
                float u = (atan2(delta.x, delta.y) + PI) / TWO_PI;
                float clear = RayDistance(u);
                float open = 1.0 - smoothstep(clear, clear + soft, span);

                // 주변 원형: 반경 경계를 안팎으로 부드럽게 넘깁니다.
                float radius = _NomadVisibilityParams.w;
                float nearby = radius > 0 ? 1.0 - smoothstep(radius - soft * 0.5, radius + soft * 0.5, span) : 0;

                // 부채꼴: 좌우 경계선까지의 수직 거리와 끝 거리로 원형과 같은 폭만큼 부드럽게 넘깁니다.
                float2 forward = _NomadVisibilityForward.xz;
                float angle = abs(atan2(forward.y * delta.x - forward.x * delta.y, dot(forward, delta)));
                float outside = max(angle - _NomadVisibilityParams.y * 0.5, 0);
                float lateral = outside >= HALF_PI ? span : span * sin(outside);
                float cone = (1.0 - smoothstep(0, soft, lateral)) *
                    (1.0 - smoothstep(_NomadVisibilityParams.x - soft * 0.5, _NomadVisibilityParams.x + soft * 0.5, span));

                float visible = open * max(nearby, cone);
                return half4(_ShadeColor.rgb, _ShadeColor.a * (1.0 - visible));
            }
            ENDHLSL
        }
    }
}
