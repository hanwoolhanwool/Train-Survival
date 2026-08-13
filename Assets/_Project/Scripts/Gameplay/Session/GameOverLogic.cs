using System.Collections.Generic;

namespace Game.Gameplay.Session
{
    /// <summary>
    /// 전멸 판정 순수 로직 (M6 3차 결정 ② — 기획서 §9.1 "전원 사망 시 게임오버").
    /// 입력은 <b>접속 중인</b> 플레이어들의 상태다 — 끊긴 플레이어는 목록에 없으므로
    /// 판정에서 자동 제외된다 (네트워크 §2.3 "접속 끊김 ≠ 사망"). 부활 대기 중은
    /// 죽어 있는 상태로 들어온다 (생존자 0이면 진행 중인 부활 대기도 무효 — 결정 ②).
    /// </summary>
    public static class GameOverLogic
    {
        /// <summary>접속자 1명의 생사 판정 입력.</summary>
        public readonly struct PlayerLifeState
        {
            /// <summary>플레이어 오브젝트가 스폰돼 있는가 — 접속 승인~스폰 사이·재접속 복원
            /// 직전에는 false다. 이때는 생사를 알 수 없으므로 판정을 보류한다(전멸 오탐 방지).</summary>
            public readonly bool HasPlayerObject;

            public readonly bool IsAlive;

            public PlayerLifeState(bool hasPlayerObject, bool isAlive)
            {
                HasPlayerObject = hasPlayerObject;
                IsAlive = isAlive;
            }
        }

        /// <summary>접속 중 전원이 죽어 있는가 — 스폰 전 접속자가 하나라도 있으면 보류(false).</summary>
        public static bool IsWipe(IReadOnlyList<PlayerLifeState> players)
        {
            if (players == null || players.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].HasPlayerObject || players[i].IsAlive)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
