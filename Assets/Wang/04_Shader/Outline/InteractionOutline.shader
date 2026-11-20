Shader "Hidden/Wang/InteractionOutline"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // C#에서 전달하는 대상의 색상, 선 두께, 마스크 텍셀 크기.
        CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float4 _OutlineTexelSize;
            float _OutlineWidth;
        CBUFFER_END

        float GetEyeDepth(float rawDepth)
        {
            if (unity_OrthoParams.w > 0.5)
                return LinearDepthToEyeDepth(rawDepth);
            return LinearEyeDepth(rawDepth, _ZBufferParams);
        }
        ENDHLSL

        // Pass 0: 보이는 대상의 깊이를 마스크에 기록한다.
        Pass
        {
            Name "TargetMask"
            Cull Back
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VS_Mask
            #pragma fragment PS_Mask
            #pragma multi_compile_instancing

            struct VS_INPUT
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VS_OUTPUT
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VS_OUTPUT VS_Mask(VS_INPUT input)
            {
                VS_OUTPUT output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // 로컬 좌표를 클립 좌표로 변환한다.
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }

            float4 PS_Mask(VS_OUTPUT input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // PS의 SV_POSITION.xy는 화면 픽셀 좌표다.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS.xy);
                float sceneDepth = GetEyeDepth(SampleSceneDepth(screenUV));
                float objectDepth = GetEyeDepth(input.positionCS.z);

                // 앞의 불투명 물체에 가려진 부분은 마스크에 그리지 않는다.
                if (objectDepth > sceneDepth + 0.01)
                    discard;

                return float4(objectDepth, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }

        // Pass 1: 대상 바깥의 픽셀만 찾아 테두리 색을 합성한다.
        Pass
        {
            Name "OutlineComposite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VS_Outline
            #pragma fragment PS_Outline

            #define USE_FULL_PRECISION_BLIT_TEXTURE
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Blit.hlsl의 Attributes/Varyings는 전체 화면 삼각형의 입출력이다.
            Varyings VS_Outline(Attributes input)
            {
                return Vert(input);
            }

            float ReadMaskDepth(float2 uv)
            {
                if (any(uv < 0.0) || any(uv > 1.0))
                    return 0.0;
                // 마스크에는 밉맵이 없으므로 반복문에서도 LOD 0을 명시한다.
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
            }

            float FindOutline(float2 screenUV, float sceneDepth)
            {
                float radius = clamp(_OutlineWidth, 1.0, 4.0);
                float radiusSquared = radius * radius;

                [loop]
                for (int y = -4; y <= 4; y++)
                {
                    [loop]
                    for (int x = -4; x <= 4; x++)
                    {
                        float2 offset = float2(x, y);
                        if (dot(offset, offset) > radiusSquared)
                            continue;

                        // 픽셀 단위 오프셋을 UV 단위로 변환한다.
                        float2 sampleUV = screenUV + offset * _OutlineTexelSize.xy;
                        float nearbyDepth = ReadMaskDepth(sampleUV);

                        // 주변에 대상이 있고 앞의 표면에 가리지 않으면 테두리다.
                        if (nearbyDepth > 0.0 && nearbyDepth <= sceneDepth + 0.01)
                            return 1.0;
                    }
                }
                return 0.0;
            }

            float4 PS_Outline(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

                // 대상 내부에는 색을 덮지 않는다.
                if (ReadMaskDepth(screenUV) > 0.0)
                    return float4(0.0, 0.0, 0.0, 0.0);

                float sceneDepth = GetEyeDepth(SampleSceneDepth(screenUV));
                float outline = FindOutline(screenUV, sceneDepth);
                return float4(_OutlineColor.rgb, _OutlineColor.a * outline);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
