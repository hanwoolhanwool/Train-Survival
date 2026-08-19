using System;

namespace Game.UI
{
    /// <summary>
    /// 배너의 급함 정도 — 같은 순간에 여러 사건이 겹치면 무엇을 남길지 정한다
    /// (비주얼·UI/UX 가이드 §9.2 D계층 "동시에 2개를 넘기지 않는다").
    /// </summary>
    internal enum HudBannerPriority
    {
        /// <summary>알아두면 좋은 것 — 지역 진입, 건축물 설치.</summary>
        Notice = 0,

        /// <summary>대비해야 하는 것 — 날씨, 다음 지역 예고.</summary>
        Warning = 1,

        /// <summary>지금 대응해야 하는 것 — 칸 파괴·이탈, 사망, 밤 시작.</summary>
        Critical = 2,
    }

    /// <summary>배너 한 건. 큐가 값으로 들고 다니므로 <c>readonly struct</c>다.</summary>
    internal readonly struct HudBanner
    {
        public readonly string Text;
        public readonly HudBannerPriority Priority;
        public readonly float ExpireTime;

        /// <summary>들어온 순서 — 같은 우선순위끼리는 <b>최신이 이긴다</b> (사건은 최근 것이 중요하다).</summary>
        public readonly int Sequence;

        public HudBanner(string text, HudBannerPriority priority, float expireTime, int sequence)
        {
            Text = text;
            Priority = priority;
            ExpireTime = expireTime;
            Sequence = sequence;
        }

        public bool IsAlive(float now) => now < ExpireTime;
    }

    /// <summary>
    /// 배너 큐 — 여러 사건이 동시에 터져도 화면에 <b>최대 2개</b>만 남긴다
    /// (비주얼·UI/UX 가이드 §9.2 D계층).
    ///
    /// <para>이전에는 배너 종류마다 <b>고정 y좌표</b>를 하나씩 들고 있었다. 그래서 동시에 나면
    /// 네 줄이 한꺼번에 쌓였고, 반대로 한 종류만 나면 화면 가운데 줄이 비었다. 이 큐는
    /// <b>자리를 종류가 아니라 순서로</b> 배분한다 — 살아 있는 배너를 급한 것부터 채운다.</para>
    ///
    /// <para><b>시간을 주입받는다</b> — <c>Time.unscaledTime</c>을 직접 읽지 않으므로 EditMode에서
    /// 시간 경계를 그대로 검증할 수 있다.</para>
    ///
    /// <para>할당이 없다: 내부 배열은 생성 시 한 번 잡고, <see cref="Resolve"/>는 호출자 버퍼를 채운다.
    /// 매 프레임 <c>OnGUI</c>에서 불리기 때문이다.</para>
    /// </summary>
    internal sealed class HudBannerQueue
    {
        /// <summary>화면에 동시에 보일 수 있는 최대 개수 (가이드 §9.2).</summary>
        public const int MaxVisible = 2;

        /// <summary>
        /// 보관 한도. 보이는 수보다 넉넉히 둬서, 급한 배너가 잠깐 자리를 차지하는 동안
        /// 뒤따라온 것이 사라지지 않게 한다. 넘치면 가장 약한 것부터 버린다.
        /// </summary>
        private const int Capacity = 8;

        private readonly HudBanner[] _items = new HudBanner[Capacity];
        private int _count;
        private int _sequence;

        /// <summary>현재 보관 중인 개수 (만료분 포함) — 테스트·진단용.</summary>
        public int StoredCount => _count;

        /// <summary>
        /// 배너를 넣는다. 빈 텍스트는 무시한다 — 표시할 것이 없는 사건까지 자리를 차지하면
        /// 정작 급한 배너가 밀린다.
        /// </summary>
        public void Push(string text, HudBannerPriority priority, float now, float holdSeconds)
        {
            if (string.IsNullOrEmpty(text) || holdSeconds <= 0f)
            {
                return;
            }

            DropExpired(now);

            if (_count == Capacity)
            {
                DropWeakest();
            }

            _items[_count++] = new HudBanner(text, priority, now + holdSeconds, _sequence++);
        }

        /// <summary>
        /// 지금 보여야 할 배너를 급한 것부터 <paramref name="destination"/>에 채우고 개수를 돌려준다.
        /// 최대 <see cref="MaxVisible"/>개이며, 버퍼가 그보다 작으면 버퍼 크기까지만 채운다.
        /// </summary>
        public int Resolve(float now, HudBanner[] destination)
        {
            if (destination == null)
            {
                return 0;
            }

            DropExpired(now);

            int limit = Math.Min(MaxVisible, destination.Length);
            int filled = 0;

            // 최대 8개라 선택 정렬로 충분하고, 정렬 사본을 만들지 않아 할당이 없다.
            for (int slot = 0; slot < limit; slot++)
            {
                int best = -1;
                for (int i = 0; i < _count; i++)
                {
                    if (IsTaken(destination, filled, i))
                    {
                        continue;
                    }

                    if (best < 0 || Outranks(_items[i], _items[best]))
                    {
                        best = i;
                    }
                }

                if (best < 0)
                {
                    break;
                }

                destination[filled++] = _items[best];
                _takenIndices[slot] = best;
            }

            return filled;
        }

        /// <summary>모두 비운다 — 씬 전환·재시작처럼 이전 사건이 의미를 잃는 순간에 쓴다.</summary>
        public void Clear()
        {
            Array.Clear(_items, 0, _count);
            _count = 0;
        }

        // Resolve가 이미 고른 항목을 다시 고르지 않도록 기록한다 (할당 없이 재사용).
        private readonly int[] _takenIndices = new int[MaxVisible];

        private bool IsTaken(HudBanner[] destination, int filled, int index)
        {
            for (int i = 0; i < filled; i++)
            {
                if (_takenIndices[i] == index)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>급한 것이 이긴다. 같으면 <b>최근에 들어온 것</b>이 이긴다.</summary>
        private static bool Outranks(HudBanner candidate, HudBanner current)
        {
            if (candidate.Priority != current.Priority)
            {
                return candidate.Priority > current.Priority;
            }

            return candidate.Sequence > current.Sequence;
        }

        private void DropExpired(float now)
        {
            int write = 0;
            for (int read = 0; read < _count; read++)
            {
                if (_items[read].IsAlive(now))
                {
                    _items[write++] = _items[read];
                }
            }

            for (int i = write; i < _count; i++)
            {
                _items[i] = default;
            }

            _count = write;
        }

        /// <summary>가장 안 급하고 가장 오래된 것을 버린다 — 새 배너에게 자리를 내주기 위해서다.</summary>
        private void DropWeakest()
        {
            int weakest = 0;
            for (int i = 1; i < _count; i++)
            {
                if (Outranks(_items[weakest], _items[i]))
                {
                    weakest = i;
                }
            }

            for (int i = weakest; i < _count - 1; i++)
            {
                _items[i] = _items[i + 1];
            }

            _items[--_count] = default;
        }
    }
}
