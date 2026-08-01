using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Cycle;
using Game.Gameplay.Inventory;
using Game.Gameplay.Monsters;
using Game.Gameplay.Player;
using Game.Gameplay.Region;
using Game.Gameplay.Train;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// M2 코어 루프 HUD — Day/국면·연료·체력·무기/탄약·처치 수 표시.
    /// UI는 상태를 소유하지 않는다: 권위/로컬 표현 이벤트 구독 + 읽기 전용 서비스 조회로 갱신만 한다.
    /// 자원 카운터·이탈 경고·조준점은 <see cref="SliceHud"/>가 담당한다 (S — 책임 분리).
    /// </summary>
    public sealed class CoreLoopHud : MonoBehaviour
    {
        private const float BannerHoldSeconds = 4f;

        private float _fuel;
        private float _fuelCapacity;
        private float _fuelConsumptionPerSecond;
        private float _health;
        private float _maxHealth;
        private HotbarItemType _selectedItem;
        private int _rounds;
        private int _capacity;
        private bool _reloading;
        private int _killCount;
        private string _bannerText;
        private float _bannerUntilTime;
        private float _deathUntilTime;
        private int _detachedCars;
        private string _trainAlertText;
        private float _trainAlertUntilTime;
        private string _regionBannerText;
        private float _regionBannerUntilTime;

        private void OnEnable()
        {
            EventBus<DayPhaseChangedEvent>.Subscribe(OnDayPhaseChanged);
            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);
            EventBus<FuelChangedEvent>.Subscribe(OnFuelChanged);
            EventBus<PlayerHealthChangedEvent>.Subscribe(OnPlayerHealthChanged);
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<HotbarSelectionChangedLocalEvent>.Subscribe(OnHotbarSelectionChanged);
            EventBus<RevolverAmmoChangedLocalEvent>.Subscribe(OnAmmoChanged);
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<CouplingBrokenEvent>.Subscribe(OnCouplingBroken);
            EventBus<CarsDetachedEvent>.Subscribe(OnCarsDetached);
            EventBus<CarDestroyedEvent>.Subscribe(OnCarDestroyed);
            EventBus<StructureDestroyedEvent>.Subscribe(OnStructureDestroyed);
            EventBus<StructureBuiltEvent>.Subscribe(OnStructureBuilt);
            EventBus<CarBuiltEvent>.Subscribe(OnCarBuilt);
        }

        private void OnDisable()
        {
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnDayPhaseChanged);
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);
            EventBus<FuelChangedEvent>.Unsubscribe(OnFuelChanged);
            EventBus<PlayerHealthChangedEvent>.Unsubscribe(OnPlayerHealthChanged);
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<HotbarSelectionChangedLocalEvent>.Unsubscribe(OnHotbarSelectionChanged);
            EventBus<RevolverAmmoChangedLocalEvent>.Unsubscribe(OnAmmoChanged);
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<CouplingBrokenEvent>.Unsubscribe(OnCouplingBroken);
            EventBus<CarsDetachedEvent>.Unsubscribe(OnCarsDetached);
            EventBus<CarDestroyedEvent>.Unsubscribe(OnCarDestroyed);
            EventBus<StructureDestroyedEvent>.Unsubscribe(OnStructureDestroyed);
            EventBus<StructureBuiltEvent>.Unsubscribe(OnStructureBuilt);
            EventBus<CarBuiltEvent>.Unsubscribe(OnCarBuilt);
        }

        private void OnDayPhaseChanged(DayPhaseChangedEvent evt)
        {
            _bannerText = evt.Phase == DayPhase.Night
                ? $"<color=red>Day {evt.DayNumber} — 밤이 온다. 열차를 지켜라!</color>"
                : $"<color=yellow>Day {evt.DayNumber} — 아침이 밝았다</color>";
            _bannerUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            string name = evt.Region == null ? $"지역 #{evt.RegionIndex}" : evt.Region.DisplayName;
            _regionBannerText = evt.CycleNumber > 0
                ? $"<color=cyan>{name} 진입 — {evt.CycleNumber + 1}주기</color>"
                : $"<color=cyan>{name} 진입</color>";
            _regionBannerUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnFuelChanged(FuelChangedEvent evt)
        {
            _fuel = evt.Fuel;
            _fuelCapacity = evt.Capacity;
            _fuelConsumptionPerSecond = evt.ConsumptionPerSecond;
        }

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _health = evt.Health;
                _maxHealth = evt.MaxHealth;
            }
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _deathUntilTime = Time.unscaledTime + BannerHoldSeconds;
            }
        }

        private void OnHotbarSelectionChanged(HotbarSelectionChangedLocalEvent evt)
        {
            _selectedItem = evt.ItemType;
        }

        private void OnAmmoChanged(RevolverAmmoChangedLocalEvent evt)
        {
            _rounds = evt.RoundsLoaded;
            _capacity = evt.Capacity;
            _reloading = evt.IsReloading;
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            _killCount += 1;
        }

        private void OnCouplingBroken(CouplingBrokenEvent evt)
        {
            _trainAlertText = $"<color=red>연결부 파괴! (#{evt.Index})</color>";
            _trainAlertUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnCarsDetached(CarsDetachedEvent evt)
        {
            int count = evt.Indices != null ? evt.Indices.Length : 0;
            _detachedCars += count;
            _trainAlertText = $"<color=red>{count}칸 이탈!</color>";
            _trainAlertUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnCarDestroyed(CarDestroyedEvent evt)
        {
            _trainAlertText = $"<color=red>칸 파괴! (#{evt.Index})</color>";
            _trainAlertUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnStructureDestroyed(StructureDestroyedEvent evt)
        {
            _trainAlertText = $"<color=red>건축물 파괴! (#{evt.Index}번 칸)</color>";
            _trainAlertUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnStructureBuilt(StructureBuiltEvent evt)
        {
            _trainAlertText = $"<color=lime>건축물 설치! (#{evt.Index}번 칸)</color>";
            _trainAlertUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnCarBuilt(CarBuiltEvent evt)
        {
            _trainAlertText = evt.Rebuilt
                ? $"<color=lime>칸 재건! (#{evt.Index})</color>"
                : $"<color=lime>칸 증설! (#{evt.Index})</color>";
            _trainAlertUntilTime = Time.unscaledTime + BannerHoldSeconds;
        }

        private void OnGUI()
        {
            DrawStatusPanel();
            DrawBanner();
        }

        private void DrawStatusPanel()
        {
            GUILayout.BeginArea(new Rect(20f, 100f, 360f, 250f));

            if (ServiceLocator.TryGet(out IDayCycleService cycle))
            {
                string phase = cycle.Phase == DayPhase.Night ? "밤" : "낮";
                int remaining = Mathf.CeilToInt(cycle.PhaseRemaining);
                GUILayout.Label($"Day {cycle.DayNumber} · {phase} (남은 시간 {remaining / 60}:{remaining % 60:00})");
            }

            DrawRegionLines();

            if (_fuelCapacity > 0f)
            {
                // 소모율을 함께 보여준다 — 칸 증설 트레이드오프(칸 수 → 소모 증가)를 눈으로 확인할 수 있다.
                string fuelText = $"연료: {_fuel:F0} / {_fuelCapacity:F0}  (-{_fuelConsumptionPerSecond:F2}/s)";
                GUILayout.Label(_fuel <= 0f ? $"<color=red>{fuelText} — 감속 중!</color>" : fuelText);
            }

            if (_maxHealth > 0f)
            {
                string healthText = $"체력: {_health:F0} / {_maxHealth:F0}";
                GUILayout.Label(_health <= _maxHealth * 0.3f ? $"<color=red>{healthText}</color>" : healthText);
            }

            if (_selectedItem == HotbarItemType.Revolver)
            {
                GUILayout.Label(_reloading ? "리볼버: 재장전 중…" : $"리볼버: {_rounds} / {_capacity}");
            }

            if (_killCount > 0)
            {
                GUILayout.Label($"처치: {_killCount}");
            }

            if (_detachedCars > 0)
            {
                GUILayout.Label($"<color=red>이탈 칸: {_detachedCars}</color>");
            }

            GUILayout.EndArea();
        }

        /// <summary>현재 지역·지역 내 일차와 다음 지역 예고 (기획서 §2 — 마지막 1~2일 예고 연출).</summary>
        private void DrawRegionLines()
        {
            if (!ServiceLocator.TryGet(out IRegionService region) || region.CurrentRegion == null)
            {
                return;
            }

            string regionLine = $"지역: {region.CurrentRegion.DisplayName} · {region.DayInRegion}/{region.RegionDayCount}일";
            if (region.CycleNumber > 0)
            {
                regionLine += $" ({region.CycleNumber + 1}주기)";
            }

            GUILayout.Label(regionLine);

            if (region.IsFinalDayOfRegion)
            {
                GUILayout.Label("<color=red>오늘 밤 — 지역 마지막 밤, 대형 웨이브</color>");
            }
            else if (region.IsForecastWindow && region.NextRegion != null)
            {
                int daysLeft = Mathf.Max(0, region.RegionDayCount - region.DayInRegion);
                GUILayout.Label($"<color=orange>다음 지역 예고: {region.NextRegion.DisplayName} ({daysLeft}일 뒤)</color>");
            }
        }

        private void DrawBanner()
        {
            if (Time.unscaledTime < _regionBannerUntilTime && !string.IsNullOrEmpty(_regionBannerText))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.14f, 400f, 30f), _regionBannerText);
            }

            if (Time.unscaledTime < _bannerUntilTime && !string.IsNullOrEmpty(_bannerText))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.2f, 400f, 30f), _bannerText);
            }

            if (Time.unscaledTime < _deathUntilTime)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.35f, 400f, 30f),
                    "<color=red>사망 — 잠시 후 후미 칸에서 부활</color>");
            }

            if (Time.unscaledTime < _trainAlertUntilTime && !string.IsNullOrEmpty(_trainAlertText))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.28f, 400f, 30f), _trainAlertText);
            }
        }
    }
}
