using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 역 소품 앵커의 레지스트리 규약 — <see cref="ResourceAnchor"/>와 같은 함정을 공유한다.
    /// <b>풀 재사용 시 사용 플래그를 리셋하지 않으면</b> 두 번째로 켜진 타일에 소품이 영영 안 놓인다.
    /// </summary>
    public sealed class StationPropAnchorTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
                _host = null;
            }

            // 정적 레지스트리는 테스트 간에 새지 않게 비운다 (프로젝트 공통 규약).
            StationPropAnchor.ClearRegistry();
        }

        private StationPropAnchor CreateAnchor()
        {
            _host = new GameObject("StationPropAnchor");
            return _host.AddComponent<StationPropAnchor>();
        }

        [Test]
        public void 사용_표시는_남았다가_재사용_초기화에서_풀린다()
        {
            // EditMode에서는 OnEnable이 돌지 않으므로 그것이 부르는 초기화를 직접 검증한다 —
            // 이 리셋을 빠뜨리면 풀에서 다시 켜진 타일이 영영 비어 있다.
            StationPropAnchor anchor = CreateAnchor();

            Assert.IsFalse(anchor.IsUsed, "처음에는 빈 자리여야 한다");

            anchor.MarkUsed();
            Assert.IsTrue(anchor.IsUsed, "심고 나면 사용 표시가 남아야 한다");

            anchor.ResetForReuse();
            Assert.IsFalse(anchor.IsUsed, "재사용 초기화가 사용 표시를 풀지 않았다");
        }

        [Test]
        public void 재사용_초기화는_여러_번_불러도_안전하다()
        {
            StationPropAnchor anchor = CreateAnchor();

            anchor.ResetForReuse();
            anchor.ResetForReuse();
            Assert.IsFalse(anchor.IsUsed);

            anchor.MarkUsed();
            anchor.MarkUsed();
            Assert.IsTrue(anchor.IsUsed);
        }

        [Test]
        public void 레지스트리_비우기는_목록만_지우고_객체를_남긴다()
        {
            StationPropAnchor.ClearRegistry();
            StationPropAnchor anchor = CreateAnchor();

            StationPropAnchor.ClearRegistry();

            Assert.AreEqual(0, StationPropAnchor.Active.Count);
            Assert.IsNotNull(anchor, "TearDown 규약이 객체까지 지우면 안 된다");
        }

        [Test]
        public void 종류와_빈자리_확률의_기본값이_저작에_안전하다()
        {
            StationPropAnchor.ClearRegistry();
            StationPropAnchor anchor = CreateAnchor();

            // 기본값이 Bin이면 저작자가 종류를 안 고른 자리에 잡동사니만 깔린다 —
            // Crate가 기본이어야 "안 고르면 평범한 상자"가 된다.
            Assert.AreEqual(StationPropKind.Crate, anchor.Kind);

            // 기본이 0이라야 저작한 자리가 조용히 비지 않는다.
            Assert.AreEqual(0f, anchor.EmptyChance);
        }
    }
}
