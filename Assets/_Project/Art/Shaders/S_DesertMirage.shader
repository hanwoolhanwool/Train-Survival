// 사막 신기루 — 지평선 왜곡 (사막 지역 구현 계획 3차 · 세계관 §4.4가 랜드마크의 짝으로 지정)
//
// 실제 신기루(하위 신기루)는 달궈진 지면 바로 위 공기층이 하늘을 반사해 "물"처럼 보이는 것이다.
// 그래서 이 셰이더는 아지랑이를 그리지 않고 **하늘색 띠**를 그린다 — 지평선 바로 위에 얇게 눕힌
// 판때기 하나에 붙여, 아래쪽은 지면에 녹고 위쪽은 하늘로 사라지게 알파를 접는다.
//
// 왜곡은 **알파와 UV를 흔드는 것**으로 만든다. 화면 전체를 굴절시키는 후처리는 렌더러 기능이라
// 파이프라인 에셋을 건드려야 하고, 그러면 이 계획의 "표현 계층만" 선을 넘는다.
//
// 카메라를 향해 세워 쓰지 않는다 — 지면에 눕히고(회전 X 90°) 멀리 두면 시선이 스치듯 지나
// 지평선 띠가 된다.
Shader "Train Survival/Desert Mirage"
{
    Properties
    {
        _BaseColor ("신기루 색 (보통 하늘·안개 색)", Color) = (0.86, 0.90, 0.94, 1)
        _Strength ("세기", Range(0, 1)) = 0.55
        _WaveScale ("물결 밀도", Range(0.5, 40)) = 9
        _WaveSpeed ("물결 속도", Range(0, 4)) = 0.55
        _EdgeSoftness ("가장자리 감쇠", Range(0.01, 1)) = 0.45
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
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 안개를 받지 않으면 모래폭풍(밀도 0.035) 중에 신기루만 흰 판때기로 남는다.
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
                float fogFactor : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Strength;
                float _WaveScale;
                float _WaveSpeed;
                float _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;

                // 물결 — 주기가 다른 사인 둘을 겹쳐 반복이 눈에 띄지 않게 한다.
                float w = sin(input.uv.x * _WaveScale + t)
                        + sin(input.uv.x * _WaveScale * 2.37 - t * 1.63) * 0.5;
                w *= 0.5;

                // 띠의 위·아래를 접는다. 흔들리는 것은 밝기가 아니라 <b>경계선</b>이다 —
                // 그래야 "수면이 일렁인다"로 읽히고 "안개가 밝아졌다 어두워진다"로 안 읽힌다.
                float edge = _EdgeSoftness;
                float band = smoothstep(0.0, edge, input.uv.y + w * 0.12)
                           * (1.0 - smoothstep(1.0 - edge, 1.0, input.uv.y + w * 0.06));

                // 신기루는 안개에 지워진다 — 폭풍이 세상을 지울 때 같이 지워져야 한다(§4.8).
                // 알파 블렌딩이라 색만 섞으면 밝기가 남으므로, 알파도 함께 접는다.
                float fogIntensity = ComputeFogIntensity(input.fogFactor);
                float a = saturate(band * _Strength * _BaseColor.a) * fogIntensity;
                float3 rgb = MixFog(_BaseColor.rgb, input.fogFactor);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
