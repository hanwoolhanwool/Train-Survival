using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// 에디터 씬 뷰 검수 전용 오브젝트 표식 — 플레이가 시작되면 스스로 꺼진다.
    /// 런타임에 같은 자리를 채우는 표현(예: 스트리밍 지형 타일의 궤도)과 겹쳐 보이는 것을 막는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EditorPreviewOnly : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.SetActive(false);
        }
    }
}
