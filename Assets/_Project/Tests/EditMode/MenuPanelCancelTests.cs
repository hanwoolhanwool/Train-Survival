using Game.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 하위 패널의 취소 경로 검증 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.3.
    ///
    /// <para><b>이 테스트가 있는 이유</b>: 4차는 "취소가 선택된 오브젝트에서 계층을 타고
    /// 올라온다"는 전제로 패널 루트에만 <see cref="ICancelHandler"/>를 달았다. 그 전제가
    /// 틀려서(입력 모듈은 <c>ExecuteHierarchy</c>가 아니라 <c>Execute</c>를 쓴다) Esc가
    /// 영영 오지 않았고, 플레이 검증 E5·I3이 실패로 올라왔다. 순수 로직이 아니라 <b>배선
    /// 규약</b>이므로 실기로만 드러났다 — 여기서 고정해 다시 새지 않게 한다.</para>
    /// </summary>
    public sealed class MenuPanelCancelTests
    {
        private GameObject _panelObject;
        private MenuPanel _panel;
        private GameObject _button;
        private GameObject _nested;

        [SetUp]
        public void SetUp()
        {
            _panelObject = new GameObject("Panel", typeof(RectTransform));
            _panel = _panelObject.AddComponent<MenuPanel>();

            _button = new GameObject("Button_Back", typeof(RectTransform), typeof(Button));
            _button.transform.SetParent(_panelObject.transform, false);

            // 한 단계 더 깊은 자식도 있다 — 계층 전파가 없으므로 깊이는 상관이 없어야 한다.
            GameObject group = new GameObject("Group", typeof(RectTransform));
            group.transform.SetParent(_panelObject.transform, false);
            _nested = new GameObject("Button_Confirm", typeof(RectTransform), typeof(Button));
            _nested.transform.SetParent(group.transform, false);

            _panelObject.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null)
            {
                Object.DestroyImmediate(_panelObject);
            }
        }

        [Test]
        public void 열면_모든_선택_항목에_취소_중계기가_붙는다()
        {
            Assert.IsNull(_button.GetComponent<MenuCancelRelay>(), "열기 전인데 이미 붙어 있다");

            _panel.Open();

            Assert.IsNotNull(_button.GetComponent<MenuCancelRelay>(), "직속 버튼에 중계기가 없다");
            Assert.IsNotNull(_nested.GetComponent<MenuCancelRelay>(), "중첩된 버튼에 중계기가 없다");
        }

        [Test]
        public void 중계기는_두_번_붙지_않는다()
        {
            _panel.Open();
            _panel.InstallCancelRelays();
            _panel.InstallCancelRelays();

            Assert.AreEqual(1, _button.GetComponents<MenuCancelRelay>().Length);
        }

        [Test]
        public void 자식_버튼이_받은_취소가_패널까지_온다()
        {
            _panel.Open();

            int cancelled = 0;
            _panel.Cancelled += delegate { cancelled++; };

            BaseEventData data = new BaseEventData(null);
            GameObject handler = ExecuteEvents.Execute(_button, data, ExecuteEvents.cancelHandler)
                ? _button
                : null;

            Assert.IsNotNull(handler, "선택된 버튼이 취소를 처리하지 못했다 — 중계기가 없다는 뜻이다");
            Assert.AreEqual(1, cancelled, "패널의 Cancelled가 오지 않았다");
            Assert.IsTrue(data.used, "취소 이벤트가 소비 표시되지 않았다");
        }

        [Test]
        public void 중첩된_자식에서도_취소가_패널까지_온다()
        {
            _panel.Open();

            int cancelled = 0;
            _panel.Cancelled += delegate { cancelled++; };

            ExecuteEvents.Execute(_nested, new BaseEventData(null), ExecuteEvents.cancelHandler);

            Assert.AreEqual(1, cancelled);
        }

        [Test]
        public void 패널_루트가_직접_받아도_동작한다()
        {
            _panel.Open();

            int cancelled = 0;
            _panel.Cancelled += delegate { cancelled++; };

            ExecuteEvents.Execute(_panelObject, new BaseEventData(null), ExecuteEvents.cancelHandler);

            Assert.AreEqual(1, cancelled);
        }
    }
}
