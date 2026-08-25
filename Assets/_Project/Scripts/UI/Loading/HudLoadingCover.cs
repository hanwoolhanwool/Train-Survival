using System.Collections.Generic;
using System.Reflection;
using Game.Core.Services;
using Game.Systems.Loading;
using UnityEngine;

namespace Game.UI.Loading
{
    /// <summary>
    /// 로딩 화면이 떠 있는 동안 <b>IMGUI HUD를 그리지 않게</b> 한다 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §8.4.
    ///
    /// <para><b>이 계획에서 가장 놓치기 쉬운 함정을 막는 것이 전부다.</b> ③ 정착 단계에서는
    /// 인게임 씬이 이미 서 있고 HUD도 그려진다. 그런데 <b>IMGUI는 uGUI 캔버스보다 항상 위에
    /// 그려지므로 <c>sortingOrder</c>로는 막을 수 없다</b> — 그대로 두면 로딩 화면 위로
    /// 핫바와 상태창이 비친다.</para>
    ///
    /// <para><b>끄고 켜는 주체는 여기 하나다.</b> 여덟 HUD 파일에 게이트 한 줄씩 넣는 대신
    /// 이 컴포넌트가 <c>enabled</c>를 내린다 — <c>OnGUI</c>가 아예 안 불린다. HUD 파일은
    /// 한 줄도 손대지 않으므로, HUD를 고치는 사람이 이 규칙을 몰라도 된다.</para>
    ///
    /// <para><b>대상은 "<c>OnGUI</c>를 가진 이웃"이다.</b> 이름으로 나열하지 않는 이유는
    /// 목록이 조용히 뒤처지기 때문이다 — 실제로 계획서가 다섯 개로 적었을 때 이미 여덟이었다.
    /// 이 오브젝트에 새 HUD가 붙으면 <b>아무것도 안 해도</b> 함께 가려진다.</para>
    ///
    /// <para><b>내가 끈 것만 되켠다.</b> 원래 꺼져 있던 HUD(조건부로 켜지는 화면)를 로딩이
    /// 끝났다고 켜 버리면 안 된다.</para>
    /// </summary>
    public sealed class HudLoadingCover : MonoBehaviour
    {
        /// <summary>이 로딩 동안 내가 끈 것들. 되켤 때 이 목록만 본다.</summary>
        private readonly List<Behaviour> _hidden = new List<Behaviour>();

        /// <summary><c>OnGUI</c>를 가진 이웃들 — 리플렉션은 한 번만 한다.</summary>
        private Behaviour[] _painters;

        private ISessionLoadFlow _flow;
        private bool _covering;

        private void Awake()
        {
            _painters = CollectPainters();
        }

        private void OnDisable()
        {
            // 이 오브젝트가 꺼지면서 HUD가 꺼진 채 남으면 다음에 아무것도 안 보인다.
            Restore();
        }

        // OnGUI보다 먼저 판정해야 한다 — LateUpdate는 그 조건을 만족하는 마지막 자리다.
        private void LateUpdate()
        {
            if (_flow == null && !ServiceLocator.TryGet(out _flow))
            {
                return;
            }

            bool shouldCover = _flow.IsActive;
            if (shouldCover == _covering)
            {
                return;
            }

            if (shouldCover)
            {
                Cover();
            }
            else
            {
                Restore();
            }
        }

        private void Cover()
        {
            _covering = true;
            _hidden.Clear();

            for (int i = 0; i < _painters.Length; i++)
            {
                Behaviour painter = _painters[i];
                if (painter == null || !painter.enabled)
                {
                    continue;
                }

                painter.enabled = false;
                _hidden.Add(painter);
            }
        }

        private void Restore()
        {
            _covering = false;

            for (int i = 0; i < _hidden.Count; i++)
            {
                if (_hidden[i] != null)
                {
                    _hidden[i].enabled = true;
                }
            }

            _hidden.Clear();
        }

        /// <summary>
        /// 이 오브젝트에서 <c>OnGUI</c>를 구현한 컴포넌트를 모은다.
        /// 씬 전체를 훑지 않는 이유는 로딩 한복판에 수천 오브젝트를 스캔하는 것이
        /// 바로 이 계획이 없애려는 종류의 렉이기 때문이다 — HUD는 한 오브젝트에 모여 있다.
        /// </summary>
        private Behaviour[] CollectPainters()
        {
            var found = new List<Behaviour>();
            MonoBehaviour[] neighbours = GetComponents<MonoBehaviour>();

            for (int i = 0; i < neighbours.Length; i++)
            {
                MonoBehaviour neighbour = neighbours[i];
                if (neighbour == null || ReferenceEquals(neighbour, this))
                {
                    continue;
                }

                MethodInfo onGui = neighbour.GetType().GetMethod(
                    "OnGUI",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (onGui != null)
                {
                    found.Add(neighbour);
                }
            }

            return found.ToArray();
        }
    }
}
