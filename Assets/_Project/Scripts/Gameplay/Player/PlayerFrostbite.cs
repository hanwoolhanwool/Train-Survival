using Game.Core.Events;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 부위별 동상 — 호스트 권위 (기획서 §4.4 북극, M7 3차 결정 ② · ⑥).
    /// <see cref="PlayerBuffs"/>와 같은 규약으로 신설한다: 서버가 진행도를 굴리고, 복제하는 것은
    /// <b>단계뿐</b>이며(부위 4개 × 2비트 = 1바이트), 사망 시 정리된다.
    ///
    /// <para>부위 4개의 진행 속도를 그 부위 <b>장비의 단열</b>이 가르고(<see cref="Inventory.PlayerInventory.GetEquippedColdInsulation"/>),
    /// 요리 보온 버프와 난방칸은 <b>네 부위 모두</b>를 늦춘다. 게임플레이 증상은 이동속도 저하
    /// 1축이고, 표현은 화면 결빙이다 — 둘 다 네 부위 단계의 <b>합계</b>에서 나온다.</para>
    ///
    /// <para>진행도(서버 전용 float 4개)는 복제하지 않는다 — 단계가 바뀔 때만
    /// <see cref="PlayerFrostbiteChangedEvent"/>가 발행된다.</para>
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerTemperature))]
    public sealed class PlayerFrostbite : NetworkBehaviour, IMoveSpeedModifier
    {
        [SerializeField] private TemperatureSettings _settings;

        private readonly NetworkVariable<byte> _stages = new NetworkVariable<byte>();

        private readonly float[] _serverProgress = new float[FrostbiteMath.PartCount];

        private PlayerHealth _health;
        private PlayerTemperature _temperature;
        private PlayerBuffs _buffs;
        private Inventory.PlayerInventory _inventory;

        /// <summary>부위 4개 × 2비트 비트팩 — 복제 값.</summary>
        public byte PackedStages => _stages.Value;

        /// <summary>네 부위 단계 합계 (0~8).</summary>
        public int StageSum => FrostbiteMath.SumStages(_stages.Value);

        /// <summary>
        /// 동상 이동속도 배율 (1 = 저하 없음). 이동 개입점(<see cref="NetworkPlayerController"/>)이
        /// 매 프레임 조회한다 — 복제된 단계에서 유도하므로 소유자 클라이언트에서도 유효하다.
        /// </summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                if (_settings == null || _stages.Value == 0)
                {
                    return 1f;
                }

                return FrostbiteMath.GetMoveSpeedMultiplier(StageSum, _settings.ToFrostbiteCurve());
            }
        }

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _temperature = GetComponent<PlayerTemperature>();
            _buffs = GetComponent<PlayerBuffs>();
            _inventory = GetComponent<Inventory.PlayerInventory>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerClear();
            }

            _stages.OnValueChanged += OnStagesChanged;
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);

            // 첫 표시를 위해 현재 값으로 한 번 발행한다 (복제 콜백은 변경이 있어야 오므로).
            PublishChanged(_stages.Value);
        }

        public override void OnNetworkDespawn()
        {
            _stages.OnValueChanged -= OnStagesChanged;
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
        }

        /// <summary>
        /// 서버 전용 — 동상 단계 복원 (M6 1차 재접속 스냅샷, M7 3차 추가). 버프가 스냅샷에서
        /// 의도적으로 제외된 것과 달리 동상은 <b>누적 상태</b>라 복원해야 한다. 진행도는 복제되지
        /// 않으므로 <b>단계 하단 진행도</b>로 되살린다 — 복원 직후 한 틱에 단계가 내려가지 않게.
        /// </summary>
        public void ServerRestoreStages(byte packed)
        {
            if (!IsServer || _settings == null)
            {
                return;
            }

            FrostbiteCurve curve = _settings.ToFrostbiteCurve();
            for (int i = 0; i < FrostbiteMath.PartCount; i++)
            {
                FrostbiteStage stage = FrostbiteMath.Unpack(packed, i);
                _serverProgress[i] = stage == FrostbiteStage.Severe ? curve.SevereThreshold
                    : stage == FrostbiteStage.Mild ? curve.MildThreshold
                    : 0f;
            }

            _stages.Value = packed;
        }

        /// <summary>
        /// 사망 확정 즉시 동상을 지운다 (<see cref="PlayerBuffs"/>와 같은 규약) — 부활 직후
        /// 이전 생의 동상으로 느리게 움직이지 않는다. 체온도 사망 중 정상치로 되돌아간다.
        /// </summary>
        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (IsServer && evt.ClientId == OwnerClientId)
            {
                ServerClear();
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _settings == null)
            {
                return;
            }

            if (!_health.IsAlive)
            {
                ServerClear();
                return;
            }

            FrostbiteCurve curve = _settings.ToFrostbiteCurve();
            float temperature = _temperature.ServerTemperature;
            float mitigation = ResolveBodyMitigation();

            for (int i = 0; i < FrostbiteMath.PartCount; i++)
            {
                float insulation = _inventory != null ? _inventory.GetEquippedColdInsulation(i) : 0f;
                _serverProgress[i] = FrostbiteMath.StepProgress(
                    _serverProgress[i], temperature, insulation, mitigation, curve, Time.deltaTime);
            }

            byte packed = FrostbiteMath.Pack(
                FrostbiteMath.GetStage(_serverProgress[0], curve),
                FrostbiteMath.GetStage(_serverProgress[1], curve),
                FrostbiteMath.GetStage(_serverProgress[2], curve),
                FrostbiteMath.GetStage(_serverProgress[3], curve));

            // 단계가 바뀔 때만 복제한다 — 진행도는 매 프레임 변하지만 대역폭을 쓸 이유가 없다.
            if (packed != _stages.Value)
            {
                _stages.Value = packed;
            }
        }

        /// <summary>
        /// 전신 완화 — 요리 보온 버프 + 난방칸 (결정 ⑥ "완화는 몸 전체"). 부위를 가르는 것은 옷뿐이다.
        /// </summary>
        private float ResolveBodyMitigation()
        {
            float mitigation = _buffs != null ? _buffs.ServerColdInsulationBonus : 0f;

            if (_temperature.ServerOnHeater)
            {
                mitigation += _settings.FrostbiteHeaterMitigation;
            }

            return mitigation;
        }

        private void ServerClear()
        {
            for (int i = 0; i < _serverProgress.Length; i++)
            {
                _serverProgress[i] = 0f;
            }

            if (_stages.Value != 0)
            {
                _stages.Value = 0;
            }
        }

        private void OnStagesChanged(byte previous, byte current)
        {
            PublishChanged(current);
        }

        private void PublishChanged(byte packed)
        {
            EventBus<PlayerFrostbiteChangedEvent>.Publish(
                new PlayerFrostbiteChangedEvent(OwnerClientId, IsOwner, packed));
        }
    }
}
