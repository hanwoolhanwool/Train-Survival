using Game.Systems.Networking.Lobby;

namespace Game.UI.Ready
{
    /// <summary>
    /// 난이도 스테퍼의 계산 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §6.1 · §9.1.
    ///
    /// <para><b>순환한다.</b> 어려움에서 ▶를 누르면 쉬움으로 돌아온다 — 3단계뿐이라
    /// 끝에서 막히면 "고장 났나" 싶고, 돌아오는 편이 시안의 좌우 화살표와도 맞는다.</para>
    ///
    /// <para><b>기본값은 가운데다.</b> 시안에 그려진 값이 "보통"이고(§12 미결 7번), 가운데라는
    /// 규칙으로 두면 단계 수가 바뀌어도 따라온다. 단계가 하나뿐이어도 0을 돌려주므로
    /// 나누기·나머지에서 죽지 않는다.</para>
    ///
    /// <para><b>이름은 여기가 만든다.</b> 실려 가는 값은 <see cref="GameDifficulty"/>이고
    /// (<c>Game.Systems</c>), "쉬움·보통·어려움"은 화면에 쓰는 말이라 UI가 갖는다 —
    /// 로컬라이징이 붙으면 고칠 곳이 <see cref="Name"/> 하나다.</para>
    ///
    /// <para>순수 계산만 있다 — 화면도 세션도 모른다. 그래서 EditMode로 고정된다.</para>
    /// </summary>
    internal static class DifficultyStepper
    {
        /// <summary>단계 수 — <see cref="GameDifficulty"/>의 값 개수와 같아야 한다.</summary>
        public const int Count = 3;

        /// <summary>처음 열었을 때의 단계 — 가운데다.</summary>
        public const int DefaultIndex = Count / 2;

        private static readonly string[] Names = { "쉬움", "보통", "어려움" };

        /// <summary>단계 수가 <paramref name="count"/>일 때의 기본 단계 — 가운데.</summary>
        public static int DefaultFor(int count)
        {
            return count <= 1 ? 0 : count / 2;
        }

        /// <summary>범위 밖 인덱스를 접어 넣는다. 단계 수가 0 이하면 0이다.</summary>
        public static int Clamp(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            // 음수를 그냥 % 하면 음수가 남는다 — 한 바퀴 더 돌려 양수로 만든다.
            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        /// <summary>다음 단계 (▶). 마지막에서는 처음으로 돌아온다.</summary>
        public static int Next(int index, int count)
        {
            return count <= 0 ? 0 : Clamp(Clamp(index, count) + 1, count);
        }

        /// <summary>이전 단계 (◀). 처음에서는 마지막으로 돌아간다.</summary>
        public static int Prev(int index, int count)
        {
            return count <= 0 ? 0 : Clamp(Clamp(index, count) - 1, count);
        }

        /// <summary>화면에 쓸 단계 이름. 범위 밖이면 접어 넣은 자리의 이름을 준다.</summary>
        public static string Name(int index)
        {
            return Names[Clamp(index, Names.Length)];
        }

        /// <summary>단계 번호를 실려 갈 값으로.</summary>
        public static GameDifficulty ToLevel(int index)
        {
            return (GameDifficulty)Clamp(index, Count);
        }

        /// <summary>실려 온 값을 단계 번호로.</summary>
        public static int ToIndex(GameDifficulty level)
        {
            return Clamp((int)level, Count);
        }
    }
}
