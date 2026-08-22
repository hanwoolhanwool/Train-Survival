// 스타일라이즈드 물 — 알파 가장자리 수면 (레벨 §10.9 ⑤)
//
// 깊이 텍스처를 쓰지 않는다. URP 의 _CameraDepthTexture 로 물가 거품을 내는 것이 정석이지만,
// 그러려면 렌더러 설정에 Depth Texture 를 켜야 하고 그 비용이 이 게임의 물 한 장에 비해 크다.
// 대신 **물가선을 BaseMap 의 알파가 직접 갖는다** — 못 모양이 텍스처에 그려져 있으므로
// 어떤 지형에 얹어도 가장자리가 부드럽게 사라진다.
//
// 잔물결은 같은 그레이스케일을 배율·방향·속도를 달리해 두 번 겹쳐 만든다.
// 한 겹이면 무늬가 통째로 흐르는 것이 눈에 보인다.
Shader "Train Survival/Stylized Water"
{
    Properties
    {
        _BaseMap ("못 모양 (RGB 수심 · A 물가선)", 2D) = "white" {}
        _WaterTint ("물 틴트", Color) = (1, 1, 1, 1)
        _Opacity ("불투명도", Range(0, 1)) = 0.82

        _RippleMap ("잔물결 (그레이스케일 · 심리스)", 2D) = "black" {}
        _RippleColor ("잔물결 색", Color) = (0.66, 0.81, 0.88, 1)
        _RippleStrength ("잔물결 세기", Range(0, 1)) = 0.28
        _RippleTiling ("잔물결 타일링", Range(0.5, 16)) = 4
        _RippleScrollA ("흐름 A (xy)", Vector) = (0.012, 0.020, 0, 0)
        _RippleScrollB ("흐름 B (xy)", Vector) = (-0.018, 0.009, 0, 0)
        _ShoreFade ("물가 페이드 (알파 감마)", Range(0.5, 4)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RippleMap); SAMPLER(sampler_RippleMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _WaterTint;
                float4 _RippleColor;
                float4 _RippleScrollA;
                float4 _RippleScrollB;
                float _Opacity;
                float _RippleStrength;
                float _RippleTiling;
                float _ShoreFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // 배율·방향·속도가 다른 두 겹 — 한 겹이면 무늬가 통째로 미끄러지는 게 보인다.
                float2 uvA = input.uv * _RippleTiling + _RippleScrollA.xy * _Time.y;
                float2 uvB = input.uv * _RippleTiling * 1.73 + _RippleScrollB.xy * _Time.y;
                half rip = SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, uvA).r * 0.62
                         + SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, uvB).r * 0.38;

                half3 color = base.rgb * _WaterTint.rgb;
                color += _RippleColor.rgb * rip * _RippleStrength;

                // 방향광 하나만 아주 약하게 받는다 — 낮/밤이 바뀌면 물도 같이 어두워져야 한다.
                Light sun = GetMainLight();
                half ndotl = saturate(dot(normalize(input.normalWS), sun.direction));
                color *= lerp(0.72, 1.12, ndotl) * lerp(half3(1,1,1), sun.color, 0.5);

                // 물가로 갈수록 알파를 더 빨리 떨어뜨려 경계를 흐린다.
                half alpha = pow(saturate(base.a), _ShoreFade) * _Opacity;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
