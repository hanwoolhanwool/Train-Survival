// 천막 천 — 바람에 흔들리고 바닥에 그늘을 드리운다 (천막 계획 2차)
//
// 1차의 천은 납작한 큐브였다. 2차가 바꾸는 것은 둘이다:
//  1) **흔들린다** — 정점을 사인 두 겹으로 민다. 네 모서리는 기둥에 묶여 있으므로 진폭을
//     가장자리에서 0으로 접는다(UV 중심 거리로 감쇠). 천이 기둥에서 떨어져 보이지 않게 하는 값이다.
//  2) **그늘을 드리운다** — ShadowCaster 패스에서 <b>같은 변형</b>을 적용한다. 이걸 빠뜨리면
//     천은 흔들리는데 그림자는 가만히 있어 눈이 먼저 알아챈다.
//
// 양면이다(Cull Off) — 천막은 아래에서 올려다보는 시간이 더 길다. 뒷면은 법선을 뒤집어
// 안쪽이 새카맣게 죽지 않게 한다.
Shader "Train Survival/Tent Cloth"
{
    Properties
    {
        _BaseColor ("천 색", Color) = (0.91, 0.86, 0.74, 1)
        _UnderTint ("안쪽(그늘면) 색", Color) = (0.62, 0.57, 0.48, 1)
        _WindStrength ("바람 세기 (m)", Range(0, 0.3)) = 0.045
        _WindSpeed ("바람 속도", Range(0, 6)) = 1.6
        _WindScale ("주름 밀도", Range(0.5, 12)) = 3.5
        _WeaveStrength ("올 무늬 세기", Range(0, 0.5)) = 0.12
        _WeaveScale ("올 무늬 밀도", Range(4, 200)) = 60
        _AmbientFloor ("환경광 하한 (모래 반사광)", Color) = (0.30, 0.27, 0.22, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _UnderTint;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            float _WeaveStrength;
            float _WeaveScale;
            float4 _AmbientFloor;
        CBUFFER_END

        // 천을 미는 양 — 가장자리(기둥에 묶인 곳)에서 0, 가운데에서 최대.
        // 두 방향 사인을 겹쳐 같은 자리로 되돌아오는 주기를 길게 만든다.
        float3 WindOffset(float3 positionOS, float2 uv)
        {
            float2 centered = uv - 0.5;
            float edgeFade = saturate(1.0 - dot(centered, centered) * 4.0);
            float t = _Time.y * _WindSpeed;
            float wave = sin((uv.x + uv.y) * _WindScale + t)
                       + 0.5 * sin((uv.x - uv.y) * _WindScale * 1.7 - t * 1.3);
            return float3(0, wave * _WindStrength * edgeFade, 0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionOS = IN.positionOS.xyz + WindOffset(IN.positionOS.xyz, IN.uv);

                VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            // 앞뒷면 판정은 URP 매크로를 쓴다 — bool + SV_IsFrontFace를 직접 받으면 플랫폼에
            // 따라 이 패스가 통째로 그려지지 않는다(2차 구현 중 실측: 그림자만 지고 천이 사라졌다).
            half4 frag(Varyings IN, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float facing = IS_FRONT_VFACE(frontFace, 1.0, -1.0);

                // 뒷면은 법선을 뒤집는다 — 안쪽이 죽으면 천막 아래가 동굴처럼 보인다.
                float3 normalWS = normalize(IN.normalWS) * facing;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 천은 얇아 빛이 배어 나온다 — 하프 램버트로 그늘면이 죽지 않게 하고,
                // 뒤에서 든 빛이 배어 나오는 투과를 더한다. 순수 램버트면 사막 한낮인데도
                // 천 아랫면이 새카맣게 앉아 "시원한 그늘"이 아니라 "구멍"으로 보인다.
                float ndotl = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                float transmission = saturate(-dot(normalWS, mainLight.direction)) * 0.35;
                float lighting = (ndotl + transmission) * lerp(0.55, 1.0, mainLight.shadowAttenuation);

                // 올 무늬 — 텍스처 없이 직조감을 만든다(가로·세로 격자 결).
                float weave = sin(IN.uv.x * _WeaveScale) * sin(IN.uv.y * _WeaveScale);
                float3 albedo = lerp(_UnderTint.rgb, _BaseColor.rgb, saturate(facing));
                albedo *= 1.0 + weave * _WeaveStrength;

                // SH가 비어 있는 환경(프리뷰·프로브 없는 씬)에서도 바닥 반사광 정도는 남긴다.
                float3 ambient = max(SampleSH(normalWS), _AmbientFloor.rgb);
                float3 color = albedo * (mainLight.color * lighting + ambient);
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual

            // 그림자는 윗면만 만든다 — 단면 천에서 양면을 다 캐스팅하면 자기 그림자가
            // 얼룩(acne)으로 앉는다. 아래로 지는 그늘은 윗면 하나로 충분하다.
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;

                // 그림자도 같은 바람을 탄다 — 이걸 빠뜨리면 천과 그늘이 따로 논다.
                float3 positionOS = IN.positionOS.xyz + WindOffset(IN.positionOS.xyz, IN.uv);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
#if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                float3 positionOS = IN.positionOS.xyz + WindOffset(IN.positionOS.xyz, IN.uv);
                OUT.positionCS = TransformObjectToHClip(positionOS);
                return OUT;
            }

            half4 depthFrag(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
