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
                float _HazeWidth;
                float _HorizonFalloff;
                float _AtmosphereThickness;
                float _Exposure;
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
