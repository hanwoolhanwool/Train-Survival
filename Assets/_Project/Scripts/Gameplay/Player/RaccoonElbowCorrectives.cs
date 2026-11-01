using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>Applies the raccoon elbow shapes after Animator and hand IK have moved the bones.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class RaccoonElbowCorrectives : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private SkinnedMeshRenderer _skin;
        private int _left = -1;
        private int _right = -1;

        public void Bind(Animator animator, SkinnedMeshRenderer skin)
        {
            _animator = animator;
            _skin = skin;
            CacheShapes();
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_skin == null) _skin = GetComponentInChildren<SkinnedMeshRenderer>();
            CacheShapes();
        }

        private void CacheShapes()
        {
            if (_skin == null || _skin.sharedMesh == null) return;
            _left = FindShape("COR_Elbow90.L");
            _right = FindShape("COR_Elbow90.R");
        }

        private int FindShape(string suffix)
        {
            Mesh mesh = _skin.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
                if (mesh.GetBlendShapeName(i).EndsWith(suffix, System.StringComparison.Ordinal)) return i;
            return -1;
        }

        public void Apply()
        {
            if (_skin == null || _animator == null || !_animator.isHuman || _animator.avatar == null || !_animator.avatar.isValid) return;
            // Editor pose inspection can call Apply before the play-mode Awake callback.
            if (_left < 0 && _right < 0) CacheShapes();
            ApplySide(_left, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            ApplySide(_right, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        }

        private void LateUpdate() => Apply();

        private void ApplySide(int index, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
        {
            if (index < 0) return;
            Transform a = _animator.GetBoneTransform(upper);
            Transform b = _animator.GetBoneTransform(lower);
            Transform c = _animator.GetBoneTransform(hand);
            if (a == null || b == null || c == null) return;
            float degrees = Vector3.Angle(b.position - a.position, c.position - b.position);
            _skin.SetBlendShapeWeight(index, Mathf.InverseLerp(45f, 90f, degrees) * 100f);
        }
    }
}
