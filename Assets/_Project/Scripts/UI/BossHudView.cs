using Game.Core.Events;
using Game.Gameplay.Monsters;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 지역 보스 체력 표시 (M7 2차 — 마지막 밤 한정). 보스가 스폰돼 있는 동안에만 열린다.
    /// UI는 상태를 소유하지 않는다 — 복제된 체력을 <see cref="BossHealth"/>가 이벤트로 흘려보내고
    /// 이 뷰는 그것을 그리기만 한다 (후발 접속도 스폰 이벤트를 받으므로 같은 상태로 열린다).
    /// </summary>
    public sealed class BossHudView : MonoBehaviour
    {
        [Tooltip("체력 바 폭 (화면 비율).")]
        [SerializeField, Range(0.2f, 0.9f)] private float _barWidthRatio = 0.5f;

        [SerializeField, Min(4f)] private float _barHeight = 18f;

        [SerializeField, Min(0f)] private float _topMargin = 56f;

        [Tooltip("처치 배너가 남아 있는 시간 (초).")]
        [SerializeField, Min(0f)] private float _killBannerSeconds = 4f;

        // 체력바는 면(fill)이라 위험 텍스트 변형이 아니라 면색을 쓴다 (비주얼·UI/UX 가이드 §7.2).
        private static readonly Color BarBackColor = UiPalette.PanelBackdrop;
        private static readonly Color BarFillColor = UiPalette.CriticalFill;

        private bool _visible;
        private string _displayName = string.Empty;
        private float _current;
        private float _max;
        private int _phaseIndex;
        private int _phaseCount = 1;

        private string _killBanner;
        private float _killBannerUntil;

        private GUIStyle _labelStyle;

        private void OnEnable()
        {
            EventBus<BossSpawnedEvent>.Subscribe(OnBossSpawned);
            EventBus<BossHealthChangedEvent>.Subscribe(OnBossHealthChanged);
            EventBus<BossPhaseChangedEvent>.Subscribe(OnBossPhaseChanged);
            EventBus<BossDespawnedEvent>.Subscribe(OnBossDespawned);
            EventBus<BossDiedEvent>.Subscribe(OnBossDied);
        }

        private void OnDisable()
        {
            EventBus<BossSpawnedEvent>.Unsubscribe(OnBossSpawned);
            EventBus<BossHealthChangedEvent>.Unsubscribe(OnBossHealthChanged);
            EventBus<BossPhaseChangedEvent>.Unsubscribe(OnBossPhaseChanged);
            EventBus<BossDespawnedEvent>.Unsubscribe(OnBossDespawned);
            EventBus<BossDiedEvent>.Unsubscribe(OnBossDied);
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            _visible = true;
            _displayName = evt.DisplayName;
            _max = evt.MaxHealth;
            _current = evt.MaxHealth;
            _phaseCount = Mathf.Max(1, evt.PhaseCount);
            _phaseIndex = 0;
        }

        private void OnBossHealthChanged(BossHealthChangedEvent evt)
        {
            _current = evt.Current;
            _max = evt.Max;
        }

        private void OnBossPhaseChanged(BossPhaseChangedEvent evt)
        {
            _phaseIndex = evt.PhaseIndex;
        }

        private void OnBossDespawned(BossDespawnedEvent evt)
        {
            _visible = false;
        }

        private void OnBossDied(BossDiedEvent evt)
        {
            _killBanner = $"{evt.DisplayName} 격파 — 새벽이 온다";
            _killBannerUntil = Time.time + _killBannerSeconds;
        }

        private void OnGUI()
        {
            EnsureStyle();

            if (_visible && _max > 0f)
            {
                DrawBossBar();
            }

            if (_killBanner != null && Time.time < _killBannerUntil)
            {
                var rect = new Rect(0f, _topMargin + _barHeight + 24f, Screen.width, 26f);
                GUI.Label(rect, $"<color={UiPalette.HexFocusBrass}><b>{_killBanner}</b></color>", _labelStyle);
            }
        }

        private void DrawBossBar()
        {
            float width = Screen.width * _barWidthRatio;
            float x = (Screen.width - width) * 0.5f;

            var back = new Rect(x, _topMargin, width, _barHeight);
            DrawSolid(back, BarBackColor);

            float ratio = Mathf.Clamp01(_current / _max);
            var fill = new Rect(x, _topMargin, width * ratio, _barHeight);
            DrawSolid(fill, BarFillColor);

            string phaseLabel = _phaseCount > 1 ? $" · 페이즈 {_phaseIndex + 1}/{_phaseCount}" : string.Empty;
            var labelRect = new Rect(x, _topMargin - 22f, width, 20f);
            GUI.Label(labelRect,
                $"<color={UiPalette.HexCriticalText}><b>{_displayName}</b></color>  {Mathf.CeilToInt(_current)} / {Mathf.CeilToInt(_max)}{phaseLabel}",
                _labelStyle);
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyle()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
            };
        }
    }
}
