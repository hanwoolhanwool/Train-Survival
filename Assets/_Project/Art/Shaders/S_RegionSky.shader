// 지역 하늘 — 그라데이션 스카이박스 (레벨 3차 · 미결 ② B안)
//
// 프로퍼티가 소유자별로 갈려 있다. 이 구분이 이 셰이더의 존재 이유다.
//   [지역]   _TopColor · _HorizonColor · _HorizonFalloff  ← 머티리얼에 저작된 지역 정체성
//   [낮/밤]  _SkyTint · _GroundColor · _AtmosphereThickness · _Exposure
//            ← DayCycleVisualController 가 매 프레임 쓴다. 이름·의미를 Skybox/Procedural 과 맞춰
//              DayVisualSettings 값을 그대로 쓸 수 있게 했다.
//
// 빌트인 Skybox/Procedural 을 지역마다 쓰면 낮/밤 연출이 4값을 전부 덮어써 지역색이 남지 않고,
// 큐브맵을 쓰면 낮/밤 애니메이션이 통째로 죽는다. 이 셰이더는 지역색 위에 낮/밤을 곱해 둘 다 살린다.
Shader "Train Survival/Region Sky"
{
    Properties
    {

        _TopColor ("천정색 (지역)", Color) = (0.30, 0.52, 0.72, 1)
        _HorizonColor ("수평선색 (지역)", Color) = (0.66, 0.81, 0.88, 1)
        _HazeColor ("수평 헤이즈색 (지역)", Color) = (0.90, 0.94, 0.96, 1)
        _HazeWidth ("헤이즈 폭 (지역)", Range(0, 0.6)) = 0.13
        _GroundTint ("수평선 아래색 (지역)", Color) = (0.37, 0.44, 0.30, 1)
        _HorizonFalloff ("수평 감쇠 (지역) - 클수록 천정색이 좁아진다", Range(0.15, 4)) = 1.1


        // [오로라] 북극 계획 §4.4 — 하늘에 붙은 겹. _AuroraIntensity 0 = 꺼짐이라
        //          이미 쓰고 있는 하늘 4종은 값이 없어도 화면이 1픽셀도 바뀌지 않는다.
        _AuroraColor ("오로라색 - 대역 아래 (지역)", Color) = (0.35, 1.0, 0.62, 1)
        _AuroraTipColor ("오로라색 - 대역 위 (지역)", Color) = (0.45, 0.42, 1.0, 1)
        _AuroraIntensity ("오로라 세기 (지역) - 0 이면 꺼진다", Range(0, 3)) = 0
        _AuroraHeight ("오로라 중심 높이 (지역) - dir.y", Range(0, 1)) = 0.32
        _AuroraWidth ("오로라 대역 폭 (지역)", Range(0.01, 0.6)) = 0.16
        _AuroraWaviness ("오로라 굽이 (지역) - 중심 높이의 진폭", Range(0, 0.4)) = 0.11
        _AuroraFrequency ("오로라 굽이 밀도 (지역)", Range(0.2, 8)) = 1.7
        _AuroraSpeed ("오로라 흐름 속도 (지역)", Range(0, 1)) = 0.06

        _SkyTint ("하늘 틴트 (낮/밤) - 0.5 가 중립", Color) = (0.5, 0.5, 0.5, 1)
        _GroundColor ("지면 반구 틴트 (낮/밤) - 0.5 가 중립", Color) = (0.37, 0.35, 0.34, 1)
        _AtmosphereThickness ("대기 두께 (낮/밤) - 클수록 수평 대역이 넓다", Range(0.05, 5)) = 1
        _Exposure ("노출 (낮/밤)", Range(0, 8)) = 1.3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _HorizonColor;
                float4 _HazeColor;
                float4 _GroundTint;
                float4 _SkyTint;
                float4 _GroundColor;
                float4 _AuroraColor;
                float4 _AuroraTipColor;
                float _HazeWidth;
                float _HorizonFalloff;
                float _AtmosphereThickness;
                float _Exposure;
                float _AuroraIntensity;
                float _AuroraHeight;
                float _AuroraWidth;
                float _AuroraWaviness;
                float _AuroraFrequency;
                float _AuroraSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // 스카이박스 메시는 원점 중심이라 오브젝트 좌표가 곧 시선 방향이다.
                output.dirOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dirOS);

                // 대기가 두꺼울수록 수평 대역이 넓어진다 — 밤에 얇아지며 천정색이 내려온다.
                float spread = max(_AtmosphereThickness, 0.05);
                float t = pow(saturate(dir.y), _HorizonFalloff / spread);
                float3 sky = lerp(_HorizonColor.rgb, _TopColor.rgb, t);

                // 수평선 바로 위의 헤이즈 — 이게 없으면 하늘이 색종이처럼 붙어 보인다.
                float haze = 1.0 - smoothstep(0.0, max(_HazeWidth * spread, 0.001), saturate(dir.y));
                sky = lerp(sky, _HazeColor.rgb, haze * 0.85);

                // 하늘과 지면은 소유자가 같지만 곱해지는 값이 다르다 —
                // 하늘은 _SkyTint, 지면은 _GroundColor 가 각각 0.5 중립으로 곱해진다.
                sky *= _SkyTint.rgb * 2.0;

                // ── 오로라 대역 (북극 계획 §4.4 · 결정 ⑦ — 두 겹 중 "하늘" 겹) ──
                //
                // 하늘에 그리므로 **fog 를 받지 않는다** — 폭설(fog 0.05)로 시야가 20 m 로 닫혀도
                // 오로라는 남는다. 비용은 0 tris 이고, 시차 커튼(원경 레이어)이 뒤에서 깊이를 만든다.
                //
                // 낮/밤 틴트 **뒤에** 더한다: 발광체는 대기 산란의 결과가 아니므로 밤에 어두워지면
                // 안 된다. 북극은 하루가 어스름 하나뿐이라(계획 §6.1) 국면차를 둘 이유도 없다.
                UNITY_BRANCH
                if (_AuroraIntensity > 0.0)
                {
                    float azimuth = atan2(dir.z, dir.x);
                    float phase = azimuth * _AuroraFrequency + _Time.y * _AuroraSpeed;

                    // 파장 셋을 겹쳐 되풀이가 눈에 띄지 않게 한다 — 하나면 사인파 띠로 읽힌다.
                    float wave = sin(phase) * 0.55 + sin(phase * 1.7 + 1.3) * 0.3 + sin(phase * 2.9 + 2.6) * 0.15;
                    float center = _AuroraHeight + wave * _AuroraWaviness;

                    float offset = (dir.y - center) / max(_AuroraWidth, 0.001);
                    float band = exp(-offset * offset);

                    // 세로 결 — 오로라를 "띠"가 아니라 "커튼"으로 읽히게 하는 것이 이 항이다.
                    // 결이 바닥까지 닿아야(0 까지 내려가야) 사이가 뚫려 보인다 — 0.62 바닥을 두면
                    // 커튼이 아니라 **초록 구름**이 된다.
                    float weave = sin(azimuth * _AuroraFrequency * 5.0 - _Time.y * _AuroraSpeed * 1.7) * 0.6
                                + sin(azimuth * _AuroraFrequency * 8.3 + 2.1) * 0.4;
                    float curtain = saturate(weave * 0.5 + 0.62);

                    // 지평선 아래로는 새지 않게 한 번 더 자른다(수평선 밑은 얼음·바다다).
                    float above = smoothstep(0.0, 0.08, dir.y);

                    // 위로 갈수록 보라 — 실제 오로라의 산소·질소 발광 고도차가 만드는 색이고,
                    // 단색이면 초록 물감을 칠한 것으로 보인다.
                    float3 auroraTint = lerp(_AuroraColor.rgb, _AuroraTipColor.rgb, saturate(offset * 0.5 + 0.5));

                    sky += auroraTint * (band * curtain * above * _AuroraIntensity);
                }

                float3 ground = _GroundTint.rgb * _GroundColor.rgb * 2.0;

                // 경계를 넉넉히 흐려 띠가 보이지 않게 한다.
                float horizon = smoothstep(-0.045, 0.02, dir.y);
                float3 color = lerp(ground, sky, horizon) * _Exposure;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
