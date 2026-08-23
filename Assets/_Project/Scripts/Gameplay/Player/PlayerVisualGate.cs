using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 인게임 씬 밖에서는 플레이어의 <b>표현 계층만</b> 감춘다.
    ///
    /// <para>NGO는 접속 시점에 플레이어를 스폰하므로, 대기실(준비 화면)에서도 플레이어 오브젝트가
    /// 이미 월드에 있다. 초기 배치는 인게임 도착까지 보류되지만 <b>몸은 그대로 그려져</b>
    /// 메뉴 배경의 열차 위에 사람이 서 있는 것처럼 보인다.</para>
    ///
    /// <para><b>루트를 끄지 않는 이유</b>: 게임 로직(<c>*Controller</c>)과 <see cref="NetworkObject"/>가
    /// 전부 루트에 있다. 루트를 끄면 RPC·복제가 멈춘다. 반대로 <c>Body</c>·<c>AimPivot</c>·
    /// <c>TpWeaponHolder</c>·<c>Rope</c> 아래는 전부 표현 컴포넌트뿐이라 통째로 꺼도 안전하다.</para>
    ///
    /// <para><b>렌더러만 끄지 않는 이유</b>: 무기 뷰들(<c>GunView</c>·<c>RepairHammerView</c> 등)이
    /// 매 프레임 자기 렌더러를 되켠다. 그 위 홀더를 끄면 뷰 자체가 멈춰 되켤 일이 없다.</para>
    /// </summary>
    public sealed class PlayerVisualGate : NetworkBehaviour
    {
        [Tooltip("인게임 밖에서 감출 표현 루트 — Body · AimPivot · TpWeaponHolder · Rope. "
            + "게임 로직이 붙은 오브젝트는 넣지 않는다.")]
        [SerializeField] private GameObject[] _visualRoots;

        public override void OnNetworkSpawn()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Apply();
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Apply();
        }

        private void Apply()
        {
            // 씬 이름으로 판정한다 — 클라이언트는 호스트가 고른 씬을 NGO 씬 동기화로 받으므로
            // GameplaySceneRoute.Current 비교가 아니라 이 판정을 써야 양쪽이 같다.
            bool show = GameplaySceneRoute.IsActiveSceneGameplay();

            if (_visualRoots == null)
            {
                return;
            }

            for (int i = 0; i < _visualRoots.Length; i++)
            {
                GameObject target = _visualRoots[i];
                if (target != null && target.activeSelf != show)
                {
                    target.SetActive(show);
                }
            }
        }
    }
}
