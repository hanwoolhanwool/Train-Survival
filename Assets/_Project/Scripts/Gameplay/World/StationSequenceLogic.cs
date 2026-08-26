using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 기차역 5장 시퀀스의 배치 추첨 — 타일 인덱스 하나에서 결정론적으로 유도하는 순수 함수
    /// ([기차역 이벤트 구현 계획](docs/plans/features/기차역-이벤트-구현-계획.md) §4.2).
    ///
    /// <para><b>왜 상태를 못 쓰는가.</b> <see cref="SegmentPickLogic.PickForTile"/>과 같은 제약을
    /// 받는다 — 런타임 스트리밍(<see cref="TerrainTileStreamer"/>)과 로딩 프리웜
    /// (<see cref="GameplayPreloadPlan"/>)이 <b>같은 함수</b>를 부르고, 후발 접속자가 과거 구간을
    /// 그릴 때도 같은 답이 나와야 한다. "직전에 역이 있었나"를 기억해 두는 방식은 그래서 못 쓴다.</para>
    ///
    /// <para><b>블록 방식.</b> 인덱스를 크기 <c>blockSize</c>의 블록으로 나누고 <b>블록마다 역을
    /// 정확히 한 번</b> 놓는다. 블록 안 어디에 놓일지는 해시가 정한다. 확률 추첨의 성질(어디 나올지
    /// 모른다)은 남기면서, 순수 확률이 못 주는 두 가지를 얻는다 —</para>
    /// <list type="bullet">
    /// <item>최소 간격이 보장된다 → "연달아 나옴"이 없다</item>
    /// <item>최대 간격도 보장된다 → <b>"지역 내내 한 번도 안 나옴"이 없다</b></item>
    /// </list>
    ///
    /// <para>시작 위치를 블록의 <b>앞 절반</b>으로 제한하는 것이 그 열쇠다. 그래야 역이 자기 블록
    /// 안에서 끝나고(<see cref="StageOf"/>가 자기 블록만 보면 된다), 이웃 블록의 역과 최소
    /// <c>blockSize / 2</c> 장이 벌어진다.</para>
    /// </summary>
    public static class StationSequenceLogic
    {
        /// <summary>블록 오프셋 추첨의 해시 소금 — <see cref="SegmentPickLogic"/>의 1·2와 겹치지 않게 둔다.</summary>
        public const int OffsetSalt = 7717;

        /// <summary>좌우 미러 추첨의 해시 소금.</summary>
        public const int MirrorSalt = 9091;

        /// <summary>역이 속하지 않는 타일임을 뜻하는 단계 값.</summary>
        public const int NoStage = -1;

        /// <summary>
        /// 설정이 성립하는가 — 블록이 시퀀스의 <b>두 배</b>는 돼야 한다.
        /// 그보다 좁으면 시작 창(앞 절반)에 역이 들어가지 못해 배치가 무너진다.
        /// </summary>
        public static bool IsValidConfig(int blockSize, int stageCount)
        {
            return stageCount > 0 && blockSize >= stageCount * 2;
        }

        /// <summary>
        /// 타일 인덱스가 속한 블록 번호. 인덱스는 <b>음수가 될 수 있으므로</b>(후방 타일)
        /// 0으로 절단하는 C# 나눗셈 대신 내림(floor)으로 나눈다 — 안 그러면 원점 부근에서
        /// 블록 0이 두 배로 넓어지고 그 구간만 역이 하나 빠진다.
        /// </summary>
        public static int BlockOf(int tileIndex, int blockSize)
        {
            if (blockSize <= 0)
            {
                return 0;
            }

            int quotient = tileIndex / blockSize;
            if (tileIndex % blockSize != 0 && tileIndex < 0)
            {
                quotient--;
            }

            return quotient;
        }

        /// <summary>
        /// 블록 안에서 역 시작이 놓일 수 있는 칸 수 — 앞 절반에서 시퀀스 길이를 뺀 만큼.
        /// 최소 1(블록 맨 앞 고정)이라 설정이 빠듯해도 역이 사라지지는 않는다.
        /// </summary>
        public static int StartWindow(int blockSize, int stageCount)
        {
            if (blockSize <= 0 || stageCount <= 0)
            {
                return 1;
            }

            return Mathf.Max(1, blockSize / 2 - stageCount + 1);
        }

        /// <summary>블록 하나가 품는 역의 시작 타일 인덱스.</summary>
        public static int StationStartIndex(int block, int blockSize, int stageCount)
        {
            int window = StartWindow(blockSize, stageCount);
            int offset = Mathf.FloorToInt(SegmentPickLogic.Hash01(block, OffsetSalt) * window);

            // Hash01이 1.0에 아주 가까울 때의 방어 — 창 밖으로 나가면 역이 블록을 넘는다.
            if (offset >= window)
            {
                offset = window - 1;
            }

            return block * blockSize + offset;
        }

        /// <summary>
        /// 이 타일이 역의 몇 번째 장인가 — 역이 아니면 <see cref="NoStage"/>.
        ///
        /// <para><b>자기 블록만 본다.</b> 시작이 앞 절반으로 제한돼 있어 역의 마지막 장도
        /// <c>block·blockSize + blockSize/2 − 1</c>을 넘지 않는다. 즉 역은 절대 블록 경계를
        /// 넘지 않으므로 이웃 블록을 뒤질 필요가 없다.</para>
        /// </summary>
        public static int StageOf(int tileIndex, int blockSize, int stageCount)
        {
            if (!IsValidConfig(blockSize, stageCount))
            {
                return NoStage;
            }

            int block = BlockOf(tileIndex, blockSize);
            int start = StationStartIndex(block, blockSize, stageCount);
            int stage = tileIndex - start;

            return stage >= 0 && stage < stageCount ? stage : NoStage;
        }

        /// <summary>이 타일이 역에 속하는가.</summary>
        public static bool IsStationTile(int tileIndex, int blockSize, int stageCount)
        {
            return StageOf(tileIndex, blockSize, stageCount) != NoStage;
        }

        /// <summary>
        /// 역 전체를 좌우로 뒤집는가 — 편측 승강장이 늘 같은 쪽에만 서면 두 번째 역부터 지루하다.
        /// 궤도가 좌우 대칭이라 뒤집어도 이음매가 어긋나지 않고, 대칭 변환이라
        /// 클리어 존 판정도 보존된다(검사기는 뒤집기 전 프리팹만 보면 된다).
        /// </summary>
        public static bool IsMirrored(int stationStartIndex)
        {
            return SegmentPickLogic.Hash01(stationStartIndex, MirrorSalt) < 0.5f;
        }

        /// <summary>
        /// 이 타일이 속한 역의 시작 인덱스 — 역이 아니면 <paramref name="tileIndex"/> 그대로.
        /// 미러 판정처럼 "역 단위"로 물어야 하는 것들의 기준점이다.
        /// </summary>
        public static int StationStartOf(int tileIndex, int blockSize, int stageCount)
        {
            int stage = StageOf(tileIndex, blockSize, stageCount);
            return stage == NoStage ? tileIndex : tileIndex - stage;
        }
    }
}
