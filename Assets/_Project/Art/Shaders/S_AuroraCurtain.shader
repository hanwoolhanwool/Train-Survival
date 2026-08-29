// 오로라 커튼 — 원경 시차 겹 (북극 지역 구현 계획 §4.4 · 결정 ⑦ "두 겹")
//
// 하늘 겹(S_RegionSky 의 _Aurora* 대역)만으로는 오로라가 **하늘에 붙은 무늬**로 읽힌다.
// 지형 뒤 600 m 에 세운 반투명 커튼이 시차 0.02 로 따로 흐를 때 비로소 "저 뒤에 있는 것"이 된다.
//
// **가산 합성(One One)을 쓰는 이유** — 완료 기준이 *"커튼이 산맥을 지우지 않는다"* 이기 때문이다.
// 알파 블렌딩은 뒤의 능선을 커튼 색으로 **덮지만**, 가산은 빛을 **더할** 뿐이라 800 m 능선이
// 언제나 그대로 남는다. 렌더 큐 순서에 기대지 않고 합성 방식으로 못을 박은 것이다.
//
// **안개는 받는다** — 하늘 겹과 정반대다. 폭설(fog 0.05)에는 지워지고 하늘 겹만 남아야
// "세상이 닫혔는데 하늘은 살아 있다"가 성립한다(계획 §11 리스크 3). 가산이므로 안개는
// fog 색으로 섞는 것이 아니라 **0 으로 감쇠**시킨다 — 더할 빛이 사라지는 것이 흐려지는 것이다.
Shader "Train Survival/Aurora Curtain"
{
    Properties
    {
        _BaseColor ("커튼 아래색", Color) = (0.24, 0.95, 0.55, 1)
        _TipColor ("커튼 위색", Color) = (0.42, 0.35, 0.95, 1)
        _Intensity ("세기", Range(0, 4)) = 0.9
        _FoldScale ("주름 밀도", Range(0.5, 40)) = 7
        _FoldSpeed ("주름 흐름 속도", Range(0, 2)) = 0.12
        _FoldContrast ("주름 대비", Range(0, 1)) = 0.65
        _BottomFade ("아래 감쇠", Range(0.01, 1)) = 0.35
        _TopFade ("위 감쇠", Range(0.01, 1)) = 0.55
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

        // 가산 — 뒤에 있는 것을 지우지 않는다. ZWrite Off 라 커튼끼리도 겹쳐 쌓인다.
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogCoord : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _Intensity;
                float _FoldScale;
                float _FoldSpeed;
                float _FoldContrast;
                float _BottomFade;
                float _TopFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 세로 주름 — 파장 셋을 겹쳐 되풀이 간격을 지운다. 커튼의 정체는 이 항이다.
                float u = input.uv.x * _FoldScale + _Time.y * _FoldSpeed;
                float fold = sin(u) * 0.5 + sin(u * 1.63 + 1.1) * 0.32 + sin(u * 2.71 + 2.4) * 0.18;
                fold = saturate(fold * 0.5 + 0.5);
                fold = lerp(1.0 - _FoldContrast, 1.0, fold);

                // 위아래로 사라지게 접는다 — 밑동이 잘려 보이면 판때기가 된다.
                float v = saturate(input.uv.y);
                float bottom = smoothstep(0.0, max(_BottomFade, 0.001), v);
                float top = 1.0 - smoothstep(1.0 - max(_TopFade, 0.001), 1.0, v);

                float3 tint = lerp(_BaseColor.rgb, _TipColor.rgb, v);
                float3 color = tint * (fold * bottom * top * _Intensity);

                // 가산이므로 안개는 "섞는" 것이 아니라 "지우는" 것이다.
                color *= ComputeFogIntensity(input.fogCoord);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
