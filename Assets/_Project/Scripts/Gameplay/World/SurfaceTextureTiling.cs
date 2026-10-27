using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>Keep the top-face texture density consistent on differently sized terrain cubes.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SurfaceTextureTiling : MonoBehaviour
    {
        [SerializeField] private Material _surfaceMaterial;
        [SerializeField, Min(0.1f)] private float _metresPerRepeat = 10f;
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

        public void Configure(Material material, float metresPerRepeat)
        {
            _surfaceMaterial = material;
            _metresPerRepeat = Mathf.Max(0.1f, metresPerRepeat);
            Apply();
        }

        private void OnEnable() => Apply();

        public void Apply()
        {
            if (_surfaceMaterial == null) return;
            var properties = new MaterialPropertyBlock();
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterial != _surfaceMaterial) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, renderer.transform.lossyScale);
                Vector3 centre = Vector3.Scale(transform.InverseTransformPoint(
                    renderer.transform.TransformPoint(filter.sharedMesh.bounds.center)), transform.lossyScale);
                // UVs are on each terrain cube, so use local surface dimensions, not rotated world AABBs.
                // Unity cube top-face U/V run toward -X/-Z. Anchor their phase to this tile,
                // keeping adjacent slab tops continuous and preventing texture swimming as it scrolls.
                float repeat = Mathf.Max(0.1f, _metresPerRepeat);
                var tiling = new Vector4(Mathf.Abs(size.x) / Mathf.Max(0.1f, _metresPerRepeat),
                    Mathf.Abs(size.z) / repeat,
                    (-centre.x - Mathf.Abs(size.x) * 0.5f) / repeat,
                    (-centre.z - Mathf.Abs(size.z) * 0.5f) / repeat);
                renderer.GetPropertyBlock(properties);
                properties.SetVector(BaseMapST, tiling);
                properties.SetVector(MainTexST, tiling);
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }
    }
}
