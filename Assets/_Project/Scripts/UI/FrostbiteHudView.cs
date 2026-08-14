using Game.Core.Events;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 동상의 표시 (M7 3차 결정 ② · 계획 §0-11) — <b>화면 결빙 오버레이</b>와 <b>부위별 단계 판독</b>.
    /// 오버레이는 "지금 얼고 있다"를 즉시 알리는 표현이고, 부위 판독은 "어디가 문제인가"를 알려주는
    /// 별개의 수단이다 (합계만 보면 어느 부위를 비웠는지 알 수 없다).
    ///
    /// <para>UI는 상태를 소유하지 않는다 — <see cref="PlayerFrostbiteChangedEvent"/>만 받아 그린다.
    /// 결빙 텍스처는 아트 에셋 없이 <b>절차적 비네트</b>로 한 번 생성한다 (준비되면 교체).</para>
    /// </summary>
    public sealed class FrostbiteHudView : MonoBehaviour
    {
        [Tooltip("합계가 최대(8)일 때 화면 결빙의 최대 불투명도.")]
        [SerializeField, Range(0f, 1f)] private float _maxOverlayAlpha = 0.55f;

        [Tooltip("결빙 강도가 목표로 수렴하는 초당 속도 — 단계가 튀어도 화면은 부드럽게 변한다.")]
        [SerializeField, Min(0.01f)] private float _intensityLerpPerSecond = 1.5f;

        [Tooltip("절차적 결빙 텍스처 한 변의 픽셀 수 — 전체 화면으로 늘려 그리므로 작아도 된다.")]
        [SerializeField, Range(32, 256)] private int _overlayResolution = 128;

        [Tooltip("부위 판독을 그릴 화면 좌측 여백·상단 위치.")]
        [SerializeField] private Vector2 _partReadoutOrigin = new Vector2(20f, 360f);

        private static readonly Color FrostTint = new Color(0.72f, 0.88f, 1f, 1f);

        private static readonly string[] PartNames = { "머리", "상체", "하체", "발" };

        private byte _packedStages;
        private float _targetIntensity;
        private float _intensity;
        private Texture2D _overlayTexture;
        private GUIStyle _labelStyle;

        private void OnEnable()
        {
            EventBus<PlayerFrostbiteChangedEvent>.Subscribe(OnFrostbiteChanged);
        }

        private void OnDisable()
        {
            EventBus<PlayerFrostbiteChangedEvent>.Unsubscribe(OnFrostbiteChanged);
        }

        private void OnDestroy()
        {
            if (_overlayTexture != null)
            {
                Destroy(_overlayTexture);
                _overlayTexture = null;
            }
        }

        private void Update()
        {
            _intensity = Mathf.MoveTowards(
                _intensity, _targetIntensity, _intensityLerpPerSecond * Time.deltaTime);
        }

        private void OnFrostbiteChanged(PlayerFrostbiteChangedEvent evt)
        {
            if (!evt.IsLocalPlayer)
            {
                return;
            }

            _packedStages = evt.PackedStages;
            _targetIntensity = FrostbiteMath.GetFreezeIntensity(evt.StageSum);
        }

        private void OnGUI()
        {
            if (_intensity > 0.001f)
            {
                DrawFreezeOverlay();
            }

            if (_packedStages != 0)
            {
                DrawPartReadout();
            }
        }

        /// <summary>화면 가장자리부터 번지는 서리 — 비네트 텍스처를 전체 화면으로 늘려 한 번 그린다.</summary>
        private void DrawFreezeOverlay()
        {
            EnsureOverlayTexture();

            Color previous = GUI.color;
            GUI.color = new Color(
                FrostTint.r, FrostTint.g, FrostTint.b, _intensity * _maxOverlayAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _overlayTexture);
            GUI.color = previous;
        }

        /// <summary>부위별 단계 — 경증/중증만 줄에 올린다 (정상 부위는 읽을 것이 없다).</summary>
        private void DrawPartReadout()
        {
            EnsureStyle();

            var builder = new System.Text.StringBuilder(48);
            builder.Append("<color=#9fd8ff><b>동상</b>");

            for (int i = 0; i < FrostbiteMath.PartCount; i++)
            {
                FrostbiteStage stage = FrostbiteMath.Unpack(_packedStages, i);
                if (stage == FrostbiteStage.None)
                {
                    continue;
                }

                string label = i < PartNames.Length ? PartNames[i] : ((EquipSlot)i).ToString();
                builder.Append(stage == FrostbiteStage.Severe
                    ? $" · <color=#ff9d9d>{label} 중증</color>"
                    : $" · {label} 경증");
            }

            builder.Append("</color>");

            var rect = new Rect(_partReadoutOrigin.x, _partReadoutOrigin.y, 420f, 22f);
            GUI.Label(rect, builder.ToString(), _labelStyle);
        }

        /// <summary>
        /// 절차적 결빙 텍스처 — 화면 중심에서 멀수록 짙어지는 비네트에 성긴 결정 무늬를 얹는다.
        /// 아트 에셋이 준비되면 이 생성부만 교체하면 된다 (그리기 경로는 그대로).
        /// </summary>
        private void EnsureOverlayTexture()
        {
            if (_overlayTexture != null)
            {
                return;
            }

            int size = Mathf.Max(32, _overlayResolution);
            _overlayTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;

                    // 가장자리(=1 부근)에서만 올라오는 비네트 — 중심 40 %는 완전히 비워 시야를 남긴다.
                    float distance = Mathf.Sqrt(nx * nx + ny * ny) / Mathf.Sqrt(2f);
                    float vignette = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, distance));

                    // 결정 무늬 — 두 방향의 저주파 격자를 곱해 성에처럼 얼룩지게 한다.
                    float crystal = 0.75f + 0.25f *
                        Mathf.Abs(Mathf.Sin((nx + ny) * 9f) * Mathf.Cos((nx - ny) * 7f));

                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(vignette * crystal) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            _overlayTexture.SetPixels32(pixels);
            _overlayTexture.Apply(false, false);
        }

        private void EnsureStyle()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label) { richText = true };
        }
    }
}
