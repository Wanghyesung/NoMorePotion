Shader "Wang/WorldDistanceVignette"
{
    // 머티리얼 Inspector에서 설정할 수 있는 값들.
    // 왼쪽 이름은 C#과 HLSL에서 접근할 때 사용하는 이름이다.
    Properties
    {
        // C#에서 기준 거리 × 정신력 비율을 계산해 전달한다.
        _VisibleDistance ("Visible Distance", Float) = 25
        _FadeRatio ("Fade Ratio", Range(0.01, 1)) = 0.5
        _EffectStrength ("Effect Strength", Range(0, 1)) = 0

        // xyz에 플레이어의 월드 좌표를 넣는다.
        _PlayerPositionWS ("Player Position WS", Vector) = (0, 0, 0, 1)
    }

    SubShader
    {
        // 이 셰이더가 URP용임을 표시한다.
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "WorldDistanceVignette"

            // 출력값(안쪽 1 / 경계 1~0 / 바깥 0)을 기존 화면 RGB에 곱한다.
            // 알파나 화면 색 텍스처 복사 없이 원본 유지 / 검정을 결정한다.
            Blend DstColor Zero

            // 화면의 RGB만 바꾸고 기존 알파 채널은 유지한다.
            ColorMask RGB

            // 전체 화면에 색을 입히는 작업이므로
            // 깊이를 기록하거나 깊이에 따라 픽셀을 탈락시키지 않는다.
            ZWrite Off
            ZTest Always

            // 삼각형의 앞뒷면에 따라 그리기를 생략하지 않는다.
            Cull Off

            HLSLPROGRAM

            #pragma target 3.5

            // 정점 셰이더는 아래에서 포함하는 Blit.hlsl의 Vert를 사용한다.
            #pragma vertex Vert

            // 픽셀 셰이더는 이 파일에서 작성한 Frag를 사용한다.
            #pragma fragment Frag

            // URP의 공통 변수와 좌표 변환 함수.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 화면 전체를 덮는 삼각형용 Vert 함수와 Varyings 구조체.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // 카메라의 깊이 텍스처와 SampleSceneDepth 함수.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // 머티리얼에서 전달받는 GPU 입력값 묶음.
            // Properties는 Inspector용 설정이고,
            // 여기 선언한 변수는 HLSL 계산에서 실제로 읽는 값이다.
            CBUFFER_START(UnityPerMaterial)

                float4 _PlayerPositionWS;

                float _VisibleDistance;
                float _FadeRatio;
                float _EffectStrength;

            CBUFFER_END

            // SV_Target:
            // 반환값을 현재 화면의 출력 색상으로 사용하라는 의미.
            half4 Frag(Varyings input) : SV_Target
            {
                // Unity의 스테레오 렌더링 관련 상태를 설정한다.
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 픽셀 셰이더에서 positionCS.xy는 화면상의 픽셀 좌표다.
                // 화면 크기로 나누어 0~1 범위의 화면 UV로 만든다.
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;

                // 현재 픽셀에 보이는 표면의 깊이를 읽는다.
                // 아직 플레이어와의 거리도, 미터 단위 거리도 아니다.
                float rawDepth = SampleSceneDepth(uv);

                float depth;
                bool isBackground;

                // 그래픽 API에 따라 깊이를 저장하는 방향과 범위가 다르다.
                #if UNITY_REVERSED_Z

                    // Reversed Z 환경에서는 가까운 쪽이 1, 먼 쪽이 0이다.
                    depth = rawDepth;
                    isBackground = rawDepth <= 0.000001;

                #else

                    // 플랫폼의 NDC 깊이 범위에 맞게 변환한다.
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                    isBackground = rawDepth >= 0.999999;

                #endif

                // 깊이가 없는 배경과 하늘은 가장 먼 영역으로 취급한다.
                float mask = 1.0;

                if (!isBackground)
                {
                    // 화면 UV + 깊이 + 역 ViewProjection 행렬로
                    // 화면에 보이는 표면의 월드 좌표를 복원한다.
                    float3 worldPosition = ComputeWorldSpacePosition(
                        uv,
                        depth,
                        UNITY_MATRIX_I_VP
                    );

                    // 표면과 플레이어 사이의 XYZ 3차원 거리.
                    float distanceFromPlayer = distance(
                        worldPosition,
                        _PlayerPositionWS.xyz
                    );

                    // 현재 가시 거리의 지정 비율만큼 안쪽에서부터 부드럽게 가린다.
                    // 예: 반경 10, 비율 0.5이면 5~10 구간에서 마스크가 0~1로 증가한다.
                    float endDistance = max(0.01, _VisibleDistance);
                    float fadeWidth = endDistance * clamp(_FadeRatio, 0.01, 1.0);
                    mask = smoothstep(endDistance - fadeWidth, endDistance, distanceFromPlayer);
                }

                // 효과가 완전히 켜졌을 때 끝 반경 밖은 검정이며, 안쪽 경계는 부드럽게 연결된다.
                // Blend DstColor Zero가 기존 화면에 이 RGB 값을 곱한다.
                // 거리 마스크에 시간 보간 강도를 곱한다. 하늘도 함께 서서히 어두워진다.
                // 강도 0이면 RGB 배율 1(원본), 강도 1이면 거리 마스크를 완전히 적용한다.
                half visible = 1.0 - mask * saturate(_EffectStrength);
                return half4(visible, visible, visible, 1.0);
            }

            ENDHLSL
        }
    }
}
