using System.Collections.Generic;

namespace Game.Systems.Networking.Lobby
{
    /// <summary>
    /// 접속한 사람들을 대기실 칸에 앉히는 규칙 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §7.3 · §9.1.
    ///
    /// <para>규칙은 셋이다. <b>호스트는 언제나 첫 칸</b>, 나머지는 <b>접속 순서</b>,
    /// <b>빈자리는 뒤로 몰린다.</b> 누가 중간에 나가면 뒤가 앞으로 당겨진다 —
    /// 칸 사이에 구멍이 남으면 "저 자리는 누가 오다 만 건가"를 묻게 된다.</para>
    ///
    /// <para><b>표시 이름도 여기서 만든다.</b> 계획 §7.3의 결정대로 이번에는 Steam 퍼소나 이름을
    /// 쓰지 않고 호스트가 칸 번호로 짓는다. 그래서 <b>클라이언트가 이름을 보낼 필요가 없고</b>,
    /// 승인 페이로드(<c>ConnectionData</c>)를 건드리지 않는다 — 재접속 식별(M6 1차)이 그대로 산다.
    /// 나중에 Steam 이름을 붙일 때 고칠 곳은 <see cref="DisplayName"/> 하나뿐이다.</para>
    ///
    /// <para>계획 §6.1은 이 타입을 <c>Game.UI</c>에 두려 했지만, 이름을 짓는 일이 <b>호스트의 일</b>이라
    /// (§7.3) <c>Game.Systems</c>로 옮겼다 — 어셈블리 의존은 단방향이라 Systems가 UI를 볼 수 없다.</para>
    /// </summary>
    public static class RosterOrdering
    {
        /// <summary>칸 수 — 패널 그림이 4장 고정이고 Steam 로비도 친구 전용 4인이다.</summary>
        public const int Capacity = 4;

        private static readonly string[] Names = { "기관사", "화부", "제동수", "차장" };

        /// <summary>
        /// 칸 번호로 짓는 임시 표시 이름 — <b>증기 기관차 승무원 넷</b>이다.
        ///
        /// <para>"플레이어 1~4"에서 바꿨다(2026-08-22 사용자 지시). 고전 증기 기관차의 승무원이
        /// 정확히 넷이고 이 대기실의 칸도 넷이라, 번호 대신 <b>자리 이름</b>을 준다 —
        /// 첫 칸의 <c>HOST</c> 표시와도 어긋나지 않는다. 기관사가 몰고, 화부가 불을 때고,
        /// 제동수가 세우고, 차장이 열차 전체를 본다.</para>
        ///
        /// <para><b>직업 선택이 아니다.</b> 이 게임에 직업 개념은 없고(계획 §1.2) 이름은
        /// <b>칸 번호에서 나오는 임시 호칭</b>일 뿐이다 — Steam 퍼소나 이름이 붙으면 사라진다(§7.3).</para>
        /// </summary>
        public static string DisplayName(int slot)
        {
            return Names[slot < 0 || slot >= Names.Length ? Names.Length - 1 : slot];
        }

        /// <summary>
        /// 접속한 클라이언트들을 칸에 앉힌다.
        ///
        /// <para>정원을 넘긴 입력은 <b>말없이 잘라낸다</b> — 5번째 접속을 여기서 막을 수는 없고
        /// (막는 자리는 승인 콜백이다), 그렇다고 예외를 던지면 대기실이 통째로 죽는다.</para>
        /// </summary>
        /// <param name="connected">접속 순서대로의 클라이언트 id. 호스트가 섞여 있어도 된다.</param>
        /// <param name="hostId">호스트의 클라이언트 id.</param>
        /// <param name="slots">채워 넣을 배열. 길이가 <see cref="Capacity"/>보다 짧아도 안전하다.</param>
        /// <returns>실제로 앉힌 인원 수.</returns>
        public static int Arrange(IReadOnlyList<ulong> connected, ulong hostId, ulong[] slots)
        {
            if (slots == null)
            {
                return 0;
            }

            int limit = slots.Length < Capacity ? slots.Length : Capacity;
            int count = 0;

            if (connected != null && limit > 0 && Contains(connected, hostId))
            {
                slots[0] = hostId;
                count = 1;
            }

            for (int i = 0; connected != null && i < connected.Count && count < limit; i++)
            {
                ulong id = connected[i];
                if (id == hostId && count > 0 && slots[0] == hostId)
                {
                    continue;
                }

                slots[count] = id;
                count++;
            }

            return count;
        }

        private static bool Contains(IReadOnlyList<ulong> list, ulong value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
