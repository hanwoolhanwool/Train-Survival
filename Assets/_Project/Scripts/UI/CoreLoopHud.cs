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

        /// <summary>이 아래로 떨어지면 체력이 상시 줄에서 <b>물러나지 않는다</b> (§9.2 A계층).</summary>
        private const float LowHealthRatio = 0.3f;

        /// <summary>안전 구간에서 상시 줄이 물러나는 불투명도 — 가이드 §9.2 "투명도 40%".</summary>
        private const float RestingAlpha = 0.4f;

        [SerializeField] private StructureCatalog _structureCatalog;

        private float _fuel;
        private float _fuelCapacity;
        private float _fuelConsumptionPerSecond;
        private float _health;
        private float _maxHealth;
        private float _temperature;
        private TemperatureStress _temperatureStress;
        private float _hunger;
        private float _maxHunger;
        private HungerStress _hungerStress;
        private float _regenBuffSeconds;
        private float _warmthBuffSeconds;
        private HotbarItemType _selectedItem;
        private HotbarItemType _ammoWeapon;
        private string _ammoWeaponName;
        private int _rounds;
        private int _capacity;
        private bool _reloading;
        private int _reserveRounds;

        // 거치 무기 탄창 (M7 4차) — 핫바 선택과 무관하게 점유 중에는 이 줄이 뜬다.
        private bool _ammoMounted;
        private int _killCount;
        private int _detachedCars;

        /// <summary>사건 배너(§9.2 D계층) — 종류별 고정 자리가 아니라 <b>한 큐</b>가 자리를 배분한다.</summary>
        private readonly HudBannerQueue _banners = new HudBannerQueue();

        /// <summary>매 프레임 <see cref="HudBannerQueue.Resolve"/>가 채우는 버퍼 — 재사용해서 할당을 만들지 않는다.</summary>
        private readonly HudBanner[] _visibleBanners = new HudBanner[HudBannerQueue.MaxVisible];

        /// <summary>임계 시에만 나오는 줄(§9.2 B계층)의 등장·퇴장 — 축마다 하나씩 둔다.</summary>
        private readonly HudTransientFade _hungerFade = new HudTransientFade();
        private readonly HudTransientFade _temperatureFade = new HudTransientFade();
        private readonly HudTransientFade _buffFade = new HudTransientFade();

        private GUIStyle _bannerStyle;

        /// <summary>버프 줄이 사라지는 동안 마지막 문구를 들고 있는다 — 없으면 퇴장 중에 글자가 비어 깜빡인다.</summary>
        private string _buffText = string.Empty;

        private readonly System.Text.StringBuilder _buffBuilder = new System.Text.StringBuilder(32);

        // 지역 경고의 경계 감지용 — Update() 참조.
        private bool _wasFinalDayOfRegion;
        private bool _wasForecastWindow;

        private void OnEnable()
        {
            EventBus<DayPhaseChangedEvent>.Subscribe(OnDayPhaseChanged);
            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);
            EventBus<WeatherChangedEvent>.Subscribe(OnWeatherChanged);
            EventBus<FuelChangedEvent>.Subscribe(OnFuelChanged);
            EventBus<PlayerHealthChangedEvent>.Subscribe(OnPlayerHealthChanged);
            EventBus<PlayerTemperatureChangedEvent>.Subscribe(OnPlayerTemperatureChanged);
            EventBus<PlayerHungerChangedEvent>.Subscribe(OnPlayerHungerChanged);
            EventBus<PlayerBuffsChangedEvent>.Subscribe(OnPlayerBuffsChanged);
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<HotbarSelectionChangedLocalEvent>.Subscribe(OnHotbarSelectionChanged);
            EventBus<WeaponAmmoChangedLocalEvent>.Subscribe(OnAmmoChanged);
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<CouplingBrokenEvent>.Subscribe(OnCouplingBroken);
            EventBus<CarsDetachedEvent>.Subscribe(OnCarsDetached);
            EventBus<CarDestroyedEvent>.Subscribe(OnCarDestroyed);
            EventBus<StructureDestroyedEvent>.Subscribe(OnStructureDestroyed);
            EventBus<StructureDemolishedEvent>.Subscribe(OnStructureDemolished);
            EventBus<StructureBuiltEvent>.Subscribe(OnStructureBuilt);
            EventBus<CarBuiltEvent>.Subscribe(OnCarBuilt);
        }

        private void OnDisable()
        {
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnDayPhaseChanged);
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);
            EventBus<WeatherChangedEvent>.Unsubscribe(OnWeatherChanged);
            EventBus<FuelChangedEvent>.Unsubscribe(OnFuelChanged);
            EventBus<PlayerHealthChangedEvent>.Unsubscribe(OnPlayerHealthChanged);
            EventBus<PlayerTemperatureChangedEvent>.Unsubscribe(OnPlayerTemperatureChanged);
            EventBus<PlayerHungerChangedEvent>.Unsubscribe(OnPlayerHungerChanged);
            EventBus<PlayerBuffsChangedEvent>.Unsubscribe(OnPlayerBuffsChanged);
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<HotbarSelectionChangedLocalEvent>.Unsubscribe(OnHotbarSelectionChanged);
            EventBus<WeaponAmmoChangedLocalEvent>.Unsubscribe(OnAmmoChanged);
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<CouplingBrokenEvent>.Unsubscribe(OnCouplingBroken);
            EventBus<CarsDetachedEvent>.Unsubscribe(OnCarsDetached);
            EventBus<CarDestroyedEvent>.Unsubscribe(OnCarDestroyed);
            EventBus<StructureDestroyedEvent>.Unsubscribe(OnStructureDestroyed);
            EventBus<StructureDemolishedEvent>.Unsubscribe(OnStructureDemolished);
            EventBus<StructureBuiltEvent>.Unsubscribe(OnStructureBuilt);
            EventBus<CarBuiltEvent>.Unsubscribe(OnCarBuilt);
        }

        private void OnDayPhaseChanged(DayPhaseChangedEvent evt)
        {
            // 밤은 대응이 필요한 사건이고, 아침은 알림이다 — 같은 전환이라도 급함이 다르다.
            if (evt.Phase == DayPhase.Night)
            {
                PushBanner(
                    $"<color={UiPalette.HexCriticalText}>Day {evt.DayNumber} — 밤이 온다. 열차를 지켜라!</color>",
                    HudBannerPriority.Critical);
            }
            else
            {
                PushBanner(
                    $"<color={UiPalette.HexSafeText}>Day {evt.DayNumber} — 아침이 밝았다</color>",
                    HudBannerPriority.Notice);
            }
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            string name = evt.Region == null ? $"지역 #{evt.RegionIndex}" : evt.Region.DisplayName;
            PushBanner(
                evt.CycleNumber > 0
                    ? $"<color={UiPalette.HexFocusBrass}>{name} 진입 — {evt.CycleNumber + 1}주기</color>"
                    : $"<color={UiPalette.HexFocusBrass}>{name} 진입</color>",
                HudBannerPriority.Notice);
        }

        private void OnWeatherChanged(WeatherChangedEvent evt)
        {
            if (!evt.IsActive)
            {
                return;
            }

            PushBanner(
                $"<color={UiPalette.HexAlertText}>{evt.Weather.DisplayName} 발생 — 시야 차단·감속</color>",
                HudBannerPriority.Warning);
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

        private void OnPlayerTemperatureChanged(PlayerTemperatureChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _temperature = evt.Temperature;
                _temperatureStress = evt.Stress;
            }
        }

        private void OnPlayerHungerChanged(PlayerHungerChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _hunger = evt.Hunger;
                _maxHunger = evt.MaxHunger;
                _hungerStress = evt.Stress;
            }
        }

        private void OnPlayerBuffsChanged(PlayerBuffsChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _regenBuffSeconds = evt.RegenRemainingSeconds;
                _warmthBuffSeconds = evt.WarmthRemainingSeconds;
            }
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                PushBanner(
                    $"<color={UiPalette.HexCriticalText}>사망 — 잠시 후 후미 칸에서 부활</color>",
                    HudBannerPriority.Critical);
            }
        }

        private void OnHotbarSelectionChanged(HotbarSelectionChangedLocalEvent evt)
        {
            _selectedItem = evt.ItemType;
        }

        private void OnAmmoChanged(WeaponAmmoChangedLocalEvent evt)
        {
            _ammoWeapon = evt.Weapon;
            _ammoWeaponName = evt.WeaponName;
            _rounds = evt.RoundsLoaded;
            _capacity = evt.Capacity;
            _reloading = evt.IsReloading;
            _reserveRounds = evt.ReserveRounds;
            _ammoMounted = evt.IsMounted;
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            _killCount += 1;
        }

        private void OnCouplingBroken(CouplingBrokenEvent evt)
        {
            PushBanner($"<color={UiPalette.HexCriticalText}>연결부 파괴! (#{evt.Index})</color>", HudBannerPriority.Critical);
        }

        private void OnCarsDetached(CarsDetachedEvent evt)
        {
            int count = evt.Indices != null ? evt.Indices.Length : 0;
            _detachedCars += count;
            PushBanner($"<color={UiPalette.HexCriticalText}>{count}칸 이탈!</color>", HudBannerPriority.Critical);
        }

        private void OnCarDestroyed(CarDestroyedEvent evt)
        {
            PushBanner($"<color={UiPalette.HexCriticalText}>칸 파괴! (#{evt.Index})</color>", HudBannerPriority.Critical);
        }

        private void OnStructureDestroyed(StructureDestroyedEvent evt)
        {
            string name = _structureCatalog != null
                ? _structureCatalog.GetDisplayName(evt.Kind)
                : "건축물";
            PushBanner($"<color={UiPalette.HexCriticalText}>{name} 파괴! (#{evt.CarIndex}번 칸)</color>", HudBannerPriority.Critical);
        }

        private void OnStructureDemolished(StructureDemolishedEvent evt)
        {
            string name = _structureCatalog != null
                ? _structureCatalog.GetDisplayName(evt.Kind)
                : "건축물";
            PushBanner($"<color={UiPalette.HexCautionText}>{name} 철거 (#{evt.CarIndex}번 칸)</color>", HudBannerPriority.Notice);
        }

        private void OnStructureBuilt(StructureBuiltEvent evt)
        {
            string name = _structureCatalog != null
                ? _structureCatalog.GetDisplayName(evt.Entry.Kind)
                : "건축물";
            PushBanner($"<color={UiPalette.HexSafeText}>{name} 설치! (#{evt.Entry.CarIndex}번 칸)</color>", HudBannerPriority.Notice);
        }

        private void OnCarBuilt(CarBuiltEvent evt)
        {
            PushBanner(
                evt.Rebuilt
                    ? $"<color={UiPalette.HexSafeText}>칸 재건! (#{evt.Index})</color>"
                    : $"<color={UiPalette.HexSafeText}>칸 증설! (#{evt.Index})</color>",
                HudBannerPriority.Notice);
        }

        /// <summary>
        /// 배너를 큐에 넣는다 (§9.2 D계층). 자리는 <see cref="HudBannerQueue"/>가 배분하므로
        /// 호출부는 <b>무엇을 얼마나 급하게</b> 알릴지만 정한다.
        /// </summary>
        private void PushBanner(string text, HudBannerPriority priority)
        {
            _banners.Push(text, priority, Time.unscaledTime, BannerHoldSeconds);
        }

        /// <summary>
        /// 지역 경고를 <b>바뀌는 순간에 한 번만</b> 배너로 띄운다 (§9.2 D계층).
        ///
        /// <para>이 둘은 이벤트가 없고 <see cref="IRegionService"/>의 조회 속성이라, 여기서 경계를
        /// 감지한다. <see cref="DayPhaseChangedEvent"/> 핸들러에서 읽지 않는 이유는
        /// <c>RegionController</c>도 같은 이벤트를 구독해 <b>갱신 순서가 보장되지 않기</b> 때문이다 —
        /// 프레임마다 bool 두 개를 읽는 편이 순서에 기대는 것보다 안전하다.</para>
        /// </summary>
        private void Update()
        {
            if (!ServiceLocator.TryGet(out IRegionService region) || region.CurrentRegion == null)
            {
                return;
            }

            bool finalDay = region.IsFinalDayOfRegion;
            if (finalDay && !_wasFinalDayOfRegion)
            {
                PushBanner(
                    $"<color={UiPalette.HexCriticalText}>오늘 밤 — 지역 마지막 밤, 대형 웨이브</color>",
                    HudBannerPriority.Critical);
            }

            _wasFinalDayOfRegion = finalDay;

            bool forecast = region.IsForecastWindow && region.NextRegion != null;
            if (forecast && !_wasForecastWindow)
            {
                int daysLeft = Mathf.Max(0, region.RegionDayCount - region.DayInRegion);
                PushBanner(
                    $"<color={UiPalette.HexCautionText}>다음 지역 예고: {region.NextRegion.DisplayName} ({daysLeft}일 뒤)</color>",
                    HudBannerPriority.Warning);
            }

            _wasForecastWindow = forecast;
        }

        private void OnGUI()
        {
            DrawStatusColumn();
            DrawBanners();
        }

        /// <summary>
        /// 좌하단 상태 기둥 — <b>상시(A)</b> 와 <b>임계 시 등장(B)</b> 계층
        /// (비주얼·UI/UX 가이드 §9.2).
        ///
        /// <para>바닥 정렬이라 <b>맨 아래 줄의 위치가 고정</b>된다. 위쪽 줄이 늘고 줄어도
        /// 눈이 따라다닐 필요가 없다 — 주변 시야로 읽으려면 자리가 안 움직여야 한다.</para>
        /// </summary>
        private void DrawStatusColumn()
        {
            float now = Time.unscaledTime;
            float delta = Time.unscaledDeltaTime;

            GUILayout.BeginArea(HudLayout.StatusColumnRect());
            GUILayout.FlexibleSpace();

            // ── B계층: 임계 시에만 등장 ────────────────────────────────
            DrawBuffLine(now, delta);
            DrawTemperatureLine(now, delta);
            DrawHungerLine(now, delta);

            // ── C계층: 맥락에서만 등장 ────────────────────────────────
            DrawAmmoLine();
            DrawKillLine();

            // ── A계층: 상시 ───────────────────────────────────────────
            DrawDetachedCarsLine();
            DrawFuelLine();
            DrawHealthLine();
            DrawTimeLine();

            GUILayout.EndArea();
        }

        /// <summary>Day·국면·남은 시간 — 상시(A). 기둥의 맨 아래, 가장 안 움직이는 자리다.</summary>
        private void DrawTimeLine()
        {
            if (!ServiceLocator.TryGet(out IDayCycleService cycle))
            {
                return;
            }

            string phase = cycle.Phase == DayPhase.Night ? "밤" : "낮";
            int remaining = Mathf.CeilToInt(cycle.PhaseRemaining);
            string region = ResolveRegionSuffix();

            GUILayout.Label(
                $"Day {cycle.DayNumber} · {phase} {remaining / 60}:{remaining % 60:00}{region}");
        }

        /// <summary>
        /// 지역·일차는 시간과 <b>한 줄로 합친다</b>. 따로 두면 상시 줄이 하나 늘어나는데,
        /// 지역은 몇 분에 한 번 바뀌므로 자기 줄을 가질 만큼 자주 변하지 않는다.
        ///
        /// <para>마지막 밤 경고·다음 지역 예고·날씨는 <b>배너로 옮겼다</b> — 사건이지 상태가 아니다
        /// (이전에는 상시 줄과 배너에 <b>중복</b>으로 나왔다).</para>
        /// </summary>
        private static string ResolveRegionSuffix()
        {
            if (!ServiceLocator.TryGet(out IRegionService region) || region.CurrentRegion == null)
            {
                return string.Empty;
            }

            return $"  ·  {region.CurrentRegion.DisplayName} {region.DayInRegion}/{region.RegionDayCount}";
        }

        /// <summary>
        /// 체력 — 상시(A). 안전 구간에서는 <b>물러난다</b> (가이드 §9.2 "투명도 40%").
        /// 지우지 않는 이유는 체력이 없어진 것과 가득한 것을 구분해야 하기 때문이다.
        /// </summary>
        private void DrawHealthLine()
        {
            if (_maxHealth <= 0f)
            {
                return;
            }

            bool low = _health <= _maxHealth * LowHealthRatio;
            string text = $"체력 {_health:F0} / {_maxHealth:F0}";

            if (low)
            {
                DrawStatusLine(UiStatusLevel.Critical, text);
                return;
            }

            DrawFaded(text, RestingAlpha);
        }

        /// <summary>
        /// 연료 — 상시(A). 가이드 §9.2는 이것을 <b>E계층(월드로 이전)</b> 으로 지정했지만,
        /// 화실 불빛 연출(§9.5)이 아직 없다. 표현을 옮기기 전에 줄부터 지우면 정보가 사라지므로
        /// 그때까지는 여기 남는다. 안전 구간에서는 체력과 같이 물러난다.
        /// </summary>
        private void DrawFuelLine()
        {
            if (_fuelCapacity <= 0f)
            {
                return;
            }

            if (_fuel <= 0f)
            {
                DrawStatusLine(UiStatusLevel.Critical, $"연료 0 — 감속 중!");
                return;
            }

            // 소모율을 함께 보여준다 — 칸 증설 트레이드오프(칸 수 → 소모 증가)를 눈으로 확인할 수 있다.
            DrawFaded($"연료 {_fuel:F0} / {_fuelCapacity:F0}  (-{_fuelConsumptionPerSecond:F2}/s)", RestingAlpha);
        }

        /// <summary>
        /// 이탈 칸 — 상시(A). <b>사건</b>(칸이 떨어져 나간 순간)은 배너가 알리고,
        /// 여기 남는 것은 <b>지속 상태</b>다. 회수할 때까지 잊으면 안 되므로 사라지지 않는다.
        /// </summary>
        private void DrawDetachedCarsLine()
        {
            if (_detachedCars > 0)
            {
                DrawStatusLine(UiStatusLevel.Critical, $"이탈 칸 {_detachedCars}");
            }
        }

        /// <summary>
        /// 탄약 — 맥락(C). 든 총의 것만 그린다 (활성 총만 발행하므로
        /// "마지막 이벤트의 무기 = 현재 선택"일 때가 그 총이다).
        /// </summary>
        private void DrawAmmoLine()
        {
            // 거치 무기는 핫바 슬롯을 차지하지 않는다 — 든 무기와 맞출 대상이 애초에 없다.
            if (!_ammoMounted && (_ammoWeapon == HotbarItemType.None || _selectedItem != _ammoWeapon))
            {
                return;
            }

            string state = _reloading ? "재장전 중…" : $"{_rounds} / {_capacity}";
            GUILayout.Label($"{_ammoWeaponName}  {state}  ·  예비 {_reserveRounds}");
        }

        /// <summary>
        /// 처치 수 — 맥락(C). <b>밤에만</b> 그린다. 낮에는 늘지 않는 숫자라 자리를 차지할 이유가 없고,
        /// 밤에는 방어전이 얼마나 진행됐는지 알려준다.
        /// </summary>
        private void DrawKillLine()
        {
            if (_killCount <= 0)
            {
                return;
            }

            if (ServiceLocator.TryGet(out IDayCycleService cycle) && cycle.Phase != DayPhase.Night)
            {
                return;
            }

            DrawFaded($"처치 {_killCount}", RestingAlpha);
        }

        /// <summary>활성 요리 버프의 잔여 시간 (기획서 §7.3, M5 4차) — 임계(B)와 같은 등퇴장을 쓴다.</summary>
        private void DrawBuffLine(float now, float delta)
        {
            bool active = _regenBuffSeconds > 0f || _warmthBuffSeconds > 0f;
            float alpha = _buffFade.Evaluate(active, now, delta);

            if (alpha <= 0f)
            {
                return;
            }

            // 사라지는 동안에는 마지막 값을 그대로 들고 있어야 글자가 깜빡이지 않는다.
            if (active)
            {
                _buffText = BuildBuffText();
            }

            DrawFaded($"<color={UiPalette.HexSafeText}>{_buffText}</color>", alpha);
        }

        private string BuildBuffText()
        {
            _buffBuilder.Length = 0;
            _buffBuilder.Append("버프:");

            if (_regenBuffSeconds > 0f)
            {
                _buffBuilder.Append($" 재생 {_regenBuffSeconds:F0}s");
            }

            if (_warmthBuffSeconds > 0f)
            {
                _buffBuilder.Append($" 보온 {_warmthBuffSeconds:F0}s");
            }

            return _buffBuilder.ToString();
        }

        /// <summary>
        /// 허기 — 임계(B). 정상 범위에서는 <b>줄 자체가 없다</b>.
        /// 전에는 배가 부를 때도 "허기: 100 / 100"이 계속 떠 있었다.
        /// </summary>
        private void DrawHungerLine(float now, float delta)
        {
            bool stressed = _maxHunger > 0f && _hungerStress != HungerStress.None;
            float alpha = _hungerFade.Evaluate(stressed, now, delta);

            if (alpha <= 0f)
            {
                return;
            }

            string text = $"허기 {_hunger:F0} / {_maxHunger:F0}";
            UiStatusLevel level = _hungerStress == HungerStress.Starving
                ? UiStatusLevel.Critical
                : UiStatusLevel.Caution;
            string suffix = _hungerStress == HungerStress.Starving
                ? " — 굶주림! 체력이 깎인다"
                : " — 허기! 요리를 먹어라";

            DrawFaded($"<color={UiPalette.StatusHex(level)}>{text}{suffix}</color>", alpha);
        }

        /// <summary>체온 — 임계(B). 쾌적할 때는 줄이 없다 (기획서 §4.2 사막 낮 열사병 / 밤 급랭).</summary>
        private void DrawTemperatureLine(float now, float delta)
        {
            bool stressed = _temperature > 0f && _temperatureStress != TemperatureStress.None;
            float alpha = _temperatureFade.Evaluate(stressed, now, delta);

            if (alpha <= 0f)
            {
                return;
            }

            string text = $"체온 {_temperature:F1}℃";

            // 더위와 추위는 같은 Alert다 — 색은 위험도를, 문구는 원인을 말한다
            // (비주얼·UI/UX 가이드 §7.2 "같은 색이 게임 전체에서 같은 뜻").
            // 그늘은 추위를 막지 못한다 — 난방 건축물이 있는 칸 위가 대응 수단이다 (M5 3차).
            string suffix = _temperatureStress == TemperatureStress.Heat
                ? " — 더위! 건축물 그늘로"
                : " — 추위! 난방 칸 위로";

            DrawFaded($"<color={UiPalette.StatusHex(UiStatusLevel.Alert)}>{text}{suffix}</color>", alpha);
        }

        /// <summary>
        /// 상태 단계에 맞는 색으로 한 줄 그리기 — 단계 판정은 도메인(<see cref="HungerStress"/> 등)이
        /// 하고, 여기서는 그것을 색으로만 옮긴다 (비주얼·UI/UX 가이드 §7.2).
        /// </summary>
        private static void DrawStatusLine(UiStatusLevel level, string text)
        {
            GUILayout.Label($"<color={UiPalette.StatusHex(level)}>{text}</color>");
        }

        /// <summary>불투명도를 낮춰 한 줄 그리기 — 물러난 상시 줄과 사라지는 임계 줄이 함께 쓴다.</summary>
        private static void DrawFaded(string text, float alpha)
        {
            Color previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
            GUILayout.Label(text);
            GUI.color = previous;
        }

        /// <summary>
        /// 사건 배너 — D계층. 큐가 고른 <b>최대 2개</b>만 위에서부터 채운다
        /// (비주얼·UI/UX 가이드 §9.2).
        /// </summary>
        private void DrawBanners()
        {
            int count = _banners.Resolve(Time.unscaledTime, _visibleBanners);
            if (count <= 0)
            {
                return;
            }

            EnsureBannerStyle();

            for (int i = 0; i < count; i++)
            {
                GUI.Label(HudLayout.BannerSlotRect(i), _visibleBanners[i].Text, _bannerStyle);
            }
        }

        private void EnsureBannerStyle()
        {
            int fontSize = UiMetrics.Font(UiMetrics.ContextPrompt);
            if (_bannerStyle != null && _bannerStyle.fontSize == fontSize)
            {
                return;
            }

            // 창 크기가 바뀌면 글자 크기도 따라가야 한다 — 그때만 다시 만든다.
            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                fontSize = fontSize,
            };
        }
    }
}
