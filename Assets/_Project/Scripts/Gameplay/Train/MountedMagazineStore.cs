using System.Collections.Generic;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 거치 무기들의 장탄 보관소 — <b>서버 내부 상태</b>다 (M7 4차 결정 ⑦).
    /// 리스트에 싣지 않는 이유: 원격 피어는 남의 장탄을 알 필요가 없고, 점유자 HUD는
    /// 로컬 선반영 + 서버 확정으로 맞춘다. 엔진·네트워크 무의존이라 EditMode 대상이다.
    /// <para>
    /// 장탄은 <b>건축물 Id에 붙는다</b> — 점유가 바뀌어도 남은 탄은 무기에 남고(§2.5 점유 교대),
    /// 파괴·철거로 항목이 사라질 때만 소실한다(보따리 배출 대상이 아니다).
    /// 설치 직후는 <b>빈 탄창</b>이다 — 낮에 채워 두고 밤에 소모하는 루프가 여기서 시작한다.
    /// </para>
    /// </summary>
    public sealed class MountedMagazineStore
    {
        private readonly Dictionary<ushort, int> _rounds = new Dictionary<ushort, int>();

        /// <summary>현재 장탄 — 미등록(설치 직후)은 0이다.</summary>
        public int GetRounds(int structureId)
        {
            return structureId > 0 && _rounds.TryGetValue((ushort)structureId, out int rounds) ? rounds : 0;
        }

        /// <summary>1발 차감 — 남은 탄이 없으면 false(발사 기각).</summary>
        public bool TryConsume(int structureId)
        {
            if (structureId <= 0)
            {
                return false;
            }

            ushort key = (ushort)structureId;
            if (!_rounds.TryGetValue(key, out int rounds) || rounds <= 0)
            {
                return false;
            }

            _rounds[key] = rounds - 1;
            return true;
        }

        /// <summary>
        /// 예비 탄약에서 탄창을 채운다 — <b>실제로 채운 발수</b>를 돌려준다(호출부가 그만큼만 인벤에서 차감한다).
        /// 빈 약실과 예비량 중 작은 쪽이 상한이라, 예비가 모자라면 부분 장전으로 끝난다.
        /// </summary>
        public int Reload(int structureId, int capacity, int reserveRounds)
        {
            if (structureId <= 0 || capacity <= 0 || reserveRounds <= 0)
            {
                return 0;
            }

            ushort key = (ushort)structureId;
            int rounds = _rounds.TryGetValue(key, out int current) ? current : 0;
            int empty = capacity - rounds;
            if (empty <= 0)
            {
                return 0;
            }

            int granted = empty < reserveRounds ? empty : reserveRounds;
            _rounds[key] = rounds + granted;
            return granted;
        }

        /// <summary>남은 탄을 소실시킨다 — 건축물 파괴·철거 확정 지점에서 호출한다.</summary>
        public void Clear(int structureId)
        {
            if (structureId > 0)
            {
                _rounds.Remove((ushort)structureId);
            }
        }

        /// <summary>세션 재시작 — 전체 초기화.</summary>
        public void ClearAll()
        {
            _rounds.Clear();
        }
    }
}
