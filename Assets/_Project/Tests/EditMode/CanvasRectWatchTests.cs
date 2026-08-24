using System.Reflection;
using Game.UI;
using Game.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 캔버스 크기 감시 검증 — 빌드 첫 실행에서 로비 좌측 표지판 비율이 어긋나던 버그의 회귀 방지.
    ///
    /// <para><b>버그의 모양</b>: 빌드로 처음 로비를 열면 표지판이 화면 비율과 어긋난 채 굳고,
    /// 게임에 들어갔다 나가기로 <c>Main</c> 씬을 다시 로드하면 그제야 맞았다.</para>
    ///
    /// <para><b>원인은 두 겹이다.</b> 하나는 배너·공고대·대기실 패널이 전부 <b>점 앵커</b>
    /// (<c>anchorMin == anchorMax</c>)라 부모가 커져도 <c>OnRectTransformDimensionsChange</c>가
    /// <b>오지 않는다</b>는 것 — 아래 첫 테스트가 그 사실을 고정한다. 다른 하나는 캔버스 rect가
    /// <c>OnEnable</c>보다 늦게 확정된다는 것이다. 둘이 겹치면 "첫 계산이 곧 마지막 계산"이 된다.</para>
    ///
    /// <para>그래서 통지 대신 <see cref="CanvasRectWatch"/>로 프레임마다 캔버스를 확인한다.
    /// 두 번째 테스트가 그 따라잡기를 검증한다.</para>
    /// </summary>
    public sealed class CanvasRectWatchTests
    {
        private const float Tolerance = 0.05f;

        private GameObject _parentObject;
        private GameObject _bannerObject;

        [SetUp]
        public void SetUp()
        {
            _parentObject = new GameObject("Canvas_Scene", typeof(RectTransform));
            RectTransform parent = (RectTransform)_parentObject.transform;
            parent.anchorMin = new Vector2(0.5f, 0.5f);
            parent.anchorMax = new Vector2(0.5f, 0.5f);
            parent.sizeDelta = new Vector2(1920f, 1080f);

            _bannerObject = new GameObject("Banner", typeof(RectTransform));
            _bannerObject.transform.SetParent(_parentObject.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_bannerObject != null)
            {
                Object.DestroyImmediate(_bannerObject);
            }

            if (_parentObject != null)
            {
                Object.DestroyImmediate(_parentObject);
            }
        }

        [Test]
        public void 점_앵커_배너는_부모가_커져도_통지를_받지_못한다()
        {
            _bannerObject.AddComponent<MenuBannerView>();
            RectTransform banner = (RectTransform)_bannerObject.transform;
            Vector2 atEnable = banner.sizeDelta;

            ((RectTransform)_parentObject.transform).sizeDelta = new Vector2(1920f, 1440f);

            Assert.AreEqual(atEnable.y, banner.sizeDelta.y, Tolerance,
                "통지가 오기 시작했다면 이 테스트가 아니라 폴링 쪽을 다시 검토할 것 — 지금 전제는 '오지 않는다'다");
            Assert.AreEqual(MenuPlateLayout.BannerSize(new Vector2(1920f, 1080f)).y, banner.sizeDelta.y, Tolerance,
                "OnEnable 시점의 캔버스(1080)로 잰 값이 그대로 남아 있어야 한다");
        }

        [Test]
        public void 폴링이_뒤늦게_확정된_캔버스를_따라잡는다()
        {
            MenuBannerView view = _bannerObject.AddComponent<MenuBannerView>();
            RectTransform banner = (RectTransform)_bannerObject.transform;

            ((RectTransform)_parentObject.transform).sizeDelta = new Vector2(1920f, 1440f);
            Tick(view);

            Vector2 expected = MenuPlateLayout.BannerSize(new Vector2(1920f, 1440f));
            Assert.AreEqual(expected.y, banner.sizeDelta.y, Tolerance, "배너 높이가 새 캔버스를 따라오지 않았다");
            Assert.AreEqual(expected.x, banner.sizeDelta.x, Tolerance, "배너 폭이 새 캔버스를 따라오지 않았다");
        }

        [Test]
        public void 크기가_그대로면_다시_배치하지_않는다()
        {
            CanvasRectWatch watch = new CanvasRectWatch();
            Vector2 size = new Vector2(1920f, 1080f);

            Assert.IsTrue(watch.NeedsApply(size), "한 번도 배치하지 않았으면 배치가 필요하다");

            watch.MarkApplied(size);

            Assert.IsFalse(watch.NeedsApply(size), "같은 크기인데 다시 배치하려 한다");
        }

        [Test]
        public void 크기가_달라지면_다시_배치한다()
        {
            CanvasRectWatch watch = new CanvasRectWatch();
            watch.MarkApplied(new Vector2(1920f, 1080f));

            Assert.IsTrue(watch.NeedsApply(new Vector2(1920f, 1200f)), "세로가 달라졌는데 배치하지 않는다");
            Assert.IsTrue(watch.NeedsApply(new Vector2(2560f, 1080f)), "가로가 달라졌는데 배치하지 않는다");
        }

        [Test]
        public void 아직_서지_않은_캔버스는_변화로_치지_않는다()
        {
            CanvasRectWatch watch = new CanvasRectWatch();

            Assert.IsFalse(watch.NeedsApply(Vector2.zero), "0 크기로 배치하면 rect가 접힌다");
            Assert.IsFalse(watch.NeedsApply(new Vector2(1920f, 0f)), "한 축이라도 0이면 배치하면 안 된다");
            Assert.IsFalse(watch.NeedsApply(new Vector2(-1f, -1f)), "음수 크기로 배치하면 안 된다");
        }

        [Test]
        public void 무효화하면_같은_크기여도_다시_배치한다()
        {
            CanvasRectWatch watch = new CanvasRectWatch();
            Vector2 size = new Vector2(1920f, 1080f);
            watch.MarkApplied(size);

            watch.Invalidate();

            Assert.IsTrue(watch.NeedsApply(size), "무효화했는데도 배치를 건너뛴다");
        }

        /// <summary>
        /// 한 프레임 지나간 셈 친다. 에디터 테스트에는 게임 루프가 없어 <c>LateUpdate</c>가
        /// 저절로 돌지 않으므로, 폴링이 실제로 그 자리에 있는지를 이렇게 확인한다.
        /// </summary>
        private static void Tick(MonoBehaviour behaviour)
        {
            MethodInfo lateUpdate = behaviour.GetType().GetMethod(
                "LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(lateUpdate, $"{behaviour.GetType().Name}에 LateUpdate 폴링이 없다");
            lateUpdate.Invoke(behaviour, null);
        }
    }
}
