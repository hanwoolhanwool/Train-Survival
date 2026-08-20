using System.Collections.Generic;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 메뉴 항목 사이의 상하 이동 계산 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.1.
    ///
    /// <para><b>순환한다.</b> 맨 아래에서 아래로 가면 맨 위로 돌아온다. 항목이 넷뿐인 화면에서
    /// 끝에 걸려 멈추면 "고장 났나" 싶은 정지가 생긴다.</para>
    ///
    /// <para><b>못 누르는 항목은 건너뛴다.</b> 세션 서비스가 아직 준비되지 않으면 "게임 시작"이
    /// 잠기는데(§5.2), 그 위를 지나갈 때 화살표가 잠긴 줄에 멈추면 눌러도 반응이 없어 보인다.</para>
    ///
    /// <para>유니티의 <c>Navigation</c>은 명시 모드에서 <b>비활성 이웃을 건너뛰지 않는다.</b>
    /// 그래서 어디로 갈지는 여기서 정하고, 그 결과만 이웃 링크로 써넣는다
    /// (<see cref="MenuBannerView"/>). 계산이 순수 함수라 EditMode에서 경계를 그대로 고정한다.</para>
    /// </summary>
    internal static class MenuNavigation
    {
        /// <summary>이동할 곳이 없을 때 (전부 잠겼거나 항목이 없을 때) 돌려주는 값.</summary>
        public const int None = -1;

        /// <summary>
        /// <paramref name="current"/>에서 <paramref name="step"/> 방향으로 한 칸 — 잠긴 항목은
        /// 건너뛰고, 끝에 닿으면 반대편으로 돌아온다.
        /// </summary>
        /// <param name="step">아래로 +1, 위로 −1. 0이면 제자리에서 가장 가까운 유효 항목을 찾는다.</param>
        public static int Move(int current, IReadOnlyList<bool> interactable, int step)
        {
            int count = interactable?.Count ?? 0;
            if (count == 0)
            {
                return None;
            }

            int direction = step >= 0 ? 1 : -1;
            int start = Normalize(current, count);

            // 자기 자신을 포함해 한 바퀴만 돈다 — 전부 잠겨 있어도 무한 루프가 되지 않는다.
            for (int i = 1; i <= count; i++)
            {
                int index = Normalize(start + direction * i, count);
                if (interactable[index])
                {
                    return index;
                }
            }

            return interactable[start] ? start : None;
        }

        /// <summary>위에서부터 처음 만나는 누를 수 있는 항목 — 화면에 들어올 때 어디에 놓을지.</summary>
        public static int First(IReadOnlyList<bool> interactable)
        {
            int count = interactable?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                if (interactable[i])
                {
                    return i;
                }
            }

            return None;
        }

        /// <summary>
        /// 지금 있는 자리가 잠겼으면 가장 가까운 유효 항목으로 옮긴다 —
        /// 항목이 도중에 잠길 때(세션 준비 전) 포커스가 죽지 않게 한다.
        /// </summary>
        public static int Rescue(int current, IReadOnlyList<bool> interactable)
        {
            int count = interactable?.Count ?? 0;
            if (count == 0)
            {
                return None;
            }

            int index = Normalize(current, count);
            return interactable[index] ? index : Move(index, interactable, 1);
        }

        /// <summary>음수와 범위 초과를 순환으로 접는다.</summary>
        public static int Normalize(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
