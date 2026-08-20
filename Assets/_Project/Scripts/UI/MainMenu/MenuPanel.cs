using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 표지판 위로 열리는 하위 패널 한 장 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.3.
    ///
    /// <para><b>열릴 때 포커스를 안으로 가져온다.</b> 그러지 않으면 패널이 떠 있는데 키보드는
    /// 뒤쪽 명판을 오가고, 취소를 눌러도 패널이 아니라 배너가 받는다.</para>
    ///
    /// <para>취소(Esc·게임패드 B)는 <see cref="ICancelHandler"/>로 받는다. 입력 액션을 따로
    /// 배선할 필요는 없다 — 표준 입력 모듈이 실어 나른다.</para>
    ///
    /// <para><b>정정(7차 실측)</b>: "취소가 선택된 오브젝트에서 <b>위로 전파</b>되므로 패널이
    /// 받는다"는 4차의 전제는 <b>틀렸다.</b> 입력 모듈은 <c>ExecuteHierarchy</c>가 아니라
    /// <c>Execute</c>로 보내므로 취소는 <b>선택된 오브젝트 하나</b>에서 끝난다. 패널 안에서
    /// 선택된 것은 언제나 버튼이라, Esc가 영영 오지 않았다(검증 E5·I3 실패).
    /// 그래서 <see cref="Open"/>이 자기 밑의 모든 <c>Selectable</c>에
    /// <see cref="MenuCancelRelay"/>를 붙인다.</para>
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour, ICancelHandler
    {
        [SerializeField]
        [Tooltip("열릴 때 포커스를 받을 항목. 비면 자식에서 첫 번째를 찾는다.")]
        private Selectable _firstSelected;

        /// <summary>취소를 눌렀다 — 닫을지는 듣는 쪽(<see cref="MainMenuRoot"/>)이 정한다.</summary>
        public event Action Cancelled;

        /// <summary>지금 열려 있는가.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>패널을 열고 포커스를 안으로 가져온다.</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            InstallCancelRelays();

            Selectable first = _firstSelected != null ? _firstSelected : GetComponentInChildren<Selectable>(false);
            if (first != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        /// <summary>
        /// 패널 안의 모든 선택 가능 항목이 취소를 이 패널로 넘기게 한다.
        ///
        /// <para>씬에 손으로 달아 두지 않는 이유: 버튼을 새로 추가할 때마다 빠뜨리고,
        /// 빠뜨렸는지는 실기로 Esc를 눌러 봐야만 드러난다. 붙이는 일 자체가 멱등이라
        /// 열 때마다 불러도 값이 늘지 않는다.</para>
        /// </summary>
        public void InstallCancelRelays()
        {
            Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] != null && selectables[i].GetComponent<MenuCancelRelay>() == null)
                {
                    selectables[i].gameObject.AddComponent<MenuCancelRelay>();
                }
            }
        }

        /// <summary>패널을 닫는다. 포커스를 어디로 돌려줄지는 부르는 쪽이 정한다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (eventData != null)
            {
                eventData.Use();
            }

            Cancelled?.Invoke();
        }
    }
}
