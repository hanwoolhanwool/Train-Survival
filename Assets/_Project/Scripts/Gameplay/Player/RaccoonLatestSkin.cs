using Game.Gameplay.Debugging;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>Uses the approved v71 skin and v73 volume correction with the gameplay skeleton.</summary>
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(Animator))]
    public sealed class RaccoonLatestSkin : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer _skin;
        [SerializeField] private Mesh _latestMesh;
        [SerializeField] private Mesh _previousMesh;
        [SerializeField] private bool _useLatest = true;
        private RaccoonArmpitVolumeV73 _volume;
        private Animator _animator;

        public bool UseLatest => _useLatest;
        public Mesh LatestMesh => _latestMesh;
        public Mesh PreviousMesh => _previousMesh;
        public SkinnedMeshRenderer Skin => _skin;
        public int CorrectedVertices => _volume != null ? _volume.CorrectedVertices : 0;
        public float MaxCorrection => _volume != null ? _volume.MaxDisplacement : 0f;

        private void OnEnable()
        {
            _animator = GetComponent<Animator>();
            if (_skin == null) _skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            Rebuild();
        }

        public void SetRevision(bool latest)
        {
            if (_useLatest == latest && (!_useLatest || _volume != null)) return;
            _useLatest = latest;
            Rebuild();
        }

        private void Rebuild()
        {
            Release();
            if (_skin == null) return;
            Mesh source = _useLatest ? _latestMesh : _previousMesh;
            if (source == null) return;
            _skin.sharedMesh = source;
            if (!_useLatest || !isActiveAndEnabled) return;
            if (!source.isReadable || source.bindposes.Length != _skin.bones.Length)
            {
                Debug.LogError("Raccoon latest skin requires a readable mesh and matching bones.", this);
                return;
            }
            // This is the same pure deformation solver used by the approved review.
            // No review motor, sampler, equipment UI or input component is installed in gameplay.
            Matrix4x4 bindMap = transform.worldToLocalMatrix * _skin.transform.localToWorldMatrix;
            _volume = new RaccoonArmpitVolumeV73(source, _skin, transform, bindMap);
        }

        private void LateUpdate()
        {
            if (_volume != null && _animator != null && _animator.isActiveAndEnabled)
                _volume.Apply();
        }

        private void Release()
        {
            _volume?.Dispose();
            _volume = null;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
