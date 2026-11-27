Shader "Custom/Skybox Solid Color"
{
    Properties
    {
        _SkyTint ("Color", Color) = (0.45, 0.65, 0.95, 1)
        _Exposure ("Exposure", Range(0, 8)) = 1

        [Header(Sun)]
        _SunSize ("Sun Size", Range(0.005, 0.2)) = 0.05
        _SunSoftness ("Sun Edge Softness", Range(0, 1)) = 0.3
        _SunIntensity ("Sun Intensity", Range(0, 5)) = 1.5
        _SunGlow ("Glow Strength", Range(0, 2)) = 0.3
        _SunGlowPower ("Glow Tightness", Range(1, 256)) = 32

        [Header(Sun Halo)]
        _HaloColor ("Halo Color", Color) = (1, 0.9, 0.7, 1)
        _HaloStrength ("Halo Strength", Range(0, 1)) = 0.4
        _HaloSpread ("Halo Spread", Range(1, 64)) = 6

        [Header(Clouds)]
        _CloudTex ("Cloud Strip (RGB 음영, A 모양)", 2D) = "black" {}
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudBandMin ("Band Bottom (도)", Range(-10, 30)) = 0
        _CloudBandMax ("Band Top (도)", Range(0, 60)) = 9
        _CloudRotation ("Rotation (도)", Range(0, 360)) = 0
        _CloudSpeed ("Rotate Speed (도/초)", Range(-5, 5)) = 0.3
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTint;
                half _Exposure;
                half _SunSize;
                half _SunSoftness;
                half _SunIntensity;
                half _SunGlow;
                half _SunGlowPower;
                half4 _HaloColor;
                half _HaloStrength;
                half _HaloSpread;
                half4 _CloudColor;
                half _CloudBandMin;
                half _CloudBandMax;
                half _CloudRotation;
                half _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDir = input.positionOS.xyz;
                return output;
            }

             half4 frag(Varyings input) : SV_Target
            {
                Light sun = GetMainLight();
                float3 dir = normalize(input.viewDir);
                float cosAngle = dot(dir, sun.direction);

                half3 sky = _SkyTint.rgb * _Exposure;

                float halo = pow(saturate(cosAngle), _HaloSpread) * _HaloStrength;
                sky = lerp(sky, _HaloColor.rgb * _Exposure, halo);
                float elevation = degrees(asin(clamp(dir.y, -1.0, 1.0)));
                float v = (elevation - _CloudBandMin) / max(_CloudBandMax - _CloudBandMin, 0.01);

                if (v >= 0.0 && v <= 1.0)
                {
                    float azimuth = degrees(atan2(dir.x, dir.z)) + _CloudRotation + _CloudSpeed * _Time.y;
                    float u = azimuth / 360.0;

                    half4 cloud = SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, float2(u, v), 0);
                    half3 cloudColor = cloud.rgb * _CloudColor.rgb * _Exposure;
                    sky = lerp(sky, cloudColor, cloud.a * _CloudColor.a);
                }
                float radius = 0.5 * _SunSize * _SunSize;
                float dist = 1.0 - cosAngle;
                float disk = 1.0 - smoothstep(radius * (1.0 - _SunSoftness), radius, dist);

                float glow = pow(saturate(cosAngle), _SunGlowPower) * _SunGlow;

                half3 sunColor = sun.color * (disk * _SunIntensity + glow);

                return half4(sky + sunColor, 1);
            }
            ENDHLSL
        }
    }
}