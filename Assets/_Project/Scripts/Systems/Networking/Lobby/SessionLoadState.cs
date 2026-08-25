using System;
using Game.Core.Logging;
using Game.Systems.Loading;
using Unity.Netcode;

namespace Game.Systems.Networking.Lobby
{
    /// <summary>
    /// 로딩의 네트워크 면 — "지금 어느 단계인가"와 "누가 준비됐는가" 둘을 나른다 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §7.
    ///
    /// <para><b><see cref="LobbyRoomState"/>와 같은 <c>NetworkObject</c>에 산다</b>(§7.1).
    /// 새 프리팹을 만들지 않으므로 <c>GlobalObjectIdHash</c> 관리 대상이 늘지 않고 —
    /// 그 함정은 이미 두 번 밟았다 — <b>총원의 출처가 바로 옆에 있다.</b>
    /// 로스터·난이도와 로딩은 다른 관심사라 <b>같은 오브젝트, 다른 컴포넌트</b>로 나눈다.</para>
    ///
    /// <para><b>단계는 <see cref="NetworkVariable{T}"/>로 나른다.</b> 계획 §7.2는
    /// <c>AdvanceClientRpc</c>를 그렸지만, 값으로 두면 <b>놓칠 수가 없다</b> — 늦게 스폰된
    /// 쪽도 현재 단계를 그대로 받고, 재전송을 손으로 짤 자리가 없다. 서버가 단계를 밀어붙이는
    /// 시점은 매번 왕복 하나 이상 떨어져 있어(전원 보고를 기다린 뒤다) 값이 뭉개질 일도 없다.</para>
    ///
    /// <para><b>보고는 RPC여야 한다.</b> 클라이언트가 쓰는 값이 아니라 <b>서버에 도착하는 사건</b>이고,
    /// 서버가 보낸 이를 알아야 하기 때문이다.</para>
    ///
    /// <para><b>보고에 단계를 실어 보낸다</b>(§7.2) — 안 실으면 지연된 ① 보고가 ③ 보고로 오인된다.
    /// 판정은 <see cref="LoadingReadiness.CountsAsReport"/>가 소유한다.</para>
    /// </summary>
    public sealed class SessionLoadState : NetworkBehaviour
    {
        /// <summary>지금 살아 있는 로딩 상태. 스폰된 것은 언제나 하나뿐이다.</summary>
        public static SessionLoadState Current { get; private set; }

        /// <summary>스폰·디스폰으로 <see cref="Current"/>가 바뀌었다.</summary>
        public static event Action CurrentChanged;

        /// <summary>서버의 지시 — "이 단계를 시작하라". 전원이 이 값을 보고 움직인다.</summary>
        private readonly NetworkVariable<byte> _directive =
            new NetworkVariable<byte>((byte)LoadingStage.Idle);

        /// <summary>지금 단계를 마쳤다고 보고한 클라이언트들. 단계가 바뀌면 비운다.</summary>
        private readonly NetworkList<ulong> _reported = new NetworkList<ulong>();

        /// <summary>지시나 보고 목록이 바뀌었다 (호스트·게스트 공통).</summary>
        public event Action Changed;

        /// <summary>서버가 지시한 단계.</summary>
        public LoadingStage Directive => (LoadingStage)_directive.Value;

        /// <summary>지금 방에 있는 사람 수 — 로스터가 총원의 단일 출처다(§7.1).</summary>
        public int MemberCount => LobbyRoomState.Current == null ? 0 : LobbyRoomState.Current.MemberCount;

        /// <summary>
        /// 총원 가운데 보고한 사람 수. <b>목록 길이가 아니라 교집합</b>이다 —
        /// 나간 사람의 보고가 목록에 남아 있어도 총원을 채우지 못한다.
        /// </summary>
        public int ReportedMemberCount
        {
            get
            {
                LobbyRoomState room = LobbyRoomState.Current;
                if (room == null)
                {
                    return 0;
                }

                int count = 0;
                for (int slot = 0; slot < room.MemberCount; slot++)
                {
                    if (room.TryGetMember(slot, out ulong clientId) && HasReported(clientId))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>이 클라이언트가 지금 단계를 보고했는가.</summary>
        public bool HasReported(ulong clientId)
        {
            for (int i = 0; i < _reported.Count; i++)
            {
                if (_reported[i] == clientId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 단계를 지시한다 — <b>서버만</b>. 보고 목록을 비우므로 <b>지시 순서가 곧 대기 경계</b>다.
        ///
        /// <para><b>출발 지시에서는 비우지 않는다.</b> 비우면 화면의 점 넷이 출발 직전에
        /// 전부 ○로 되돌아간다 — 방금 "전원 준비됨"을 보여 준 화면이 마지막 순간에
        /// <b>아무도 준비 안 된 것처럼</b> 바뀌는 셈이고, 그게 제일 이상하게 읽힌다.
        /// 출발 뒤에는 기다릴 것이 없으므로 목록을 남겨 둬도 판정에 영향이 없다.</para>
        /// </summary>
        public bool BeginStage(LoadingStage stage)
        {
            if (!IsSpawned || !IsServer)
            {
                return false;
            }

            if (stage != LoadingStage.Depart)
            {
                _reported.Clear();
            }

            _directive.Value = (byte)stage;
            return true;
        }

        /// <summary>
        /// 이 단계를 마쳤다고 알린다 — 호스트도 부른다(§7.3).
        /// "서버니까 바로 진행" 같은 지름길을 만들지 않는 이유는 <b>혼자 할 때만 재현되는
        /// 버그를 만들지 않기 위해서</b>다.
        /// </summary>
        public void Report(LoadingStage stage)
        {
            if (IsSpawned)
            {
                ReportReadyServerRpc((byte)stage);
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void ReportReadyServerRpc(byte stage, RpcParams rpcParams = default)
        {
            if (!LoadingReadiness.CountsAsReport(Directive, (LoadingStage)stage))
            {
                // 지난 단계의 지연 보고 — 세면 아직 준비 안 된 사람을 데리고 출발하게 된다.
                return;
            }

            ulong sender = rpcParams.Receive.SenderClientId;
            if (!HasReported(sender))
            {
                _reported.Add(sender);
            }
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            _directive.OnValueChanged += OnDirectiveChanged;
            _reported.OnListChanged += OnReportedChanged;

            CurrentChanged?.Invoke();
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _directive.OnValueChanged -= OnDirectiveChanged;
            _reported.OnListChanged -= OnReportedChanged;

            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }

            Changed?.Invoke();
        }

        private void OnDirectiveChanged(byte previous, byte current)
        {
            GameLog.Info(LogCategory.Session, $"로딩 지시: {(LoadingStage)previous} → {(LoadingStage)current}");
            Changed?.Invoke();
        }

        private void OnReportedChanged(NetworkListEvent<ulong> _)
        {
            Changed?.Invoke();
        }
    }
}
