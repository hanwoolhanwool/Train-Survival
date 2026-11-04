#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Debugging
{
    /// <summary>Editor-only observer camera; gameplay input, networking and movement stay authoritative.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class RaccoonInGameInspection : MonoBehaviour
    {
        public bool Inspect = true;
        public float OrbitYaw = 150f;
        public float Pitch = 12f;
        public float Distance = 3.4f;
        public RaccoonLatestSkin Target { get; private set; }
        private PlayerCharacterView player;
        private Camera gameplayCamera;
        private Camera observer;
        private bool applied;
        private bool cameraWasEnabled;
        private readonly Dictionary<Renderer, ShadowCastingMode> shadows = new Dictionary<Renderer, ShadowCastingMode>();
        private readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();

        private void LateUpdate()
        {
            if (player == null)
            {
                foreach (var candidate in FindObjectsByType<PlayerCharacterView>(FindObjectsSortMode.None))
                    if (candidate.IsSpawned && candidate.IsOwner) { player = candidate; break; }
                if (player == null) return;
                Target = player.GetComponentInChildren<RaccoonLatestSkin>();
                gameplayCamera = player.GetComponentInChildren<Camera>();
            }
            if (Target == null || gameplayCamera == null) return;
            if (!Inspect) { Restore(); return; }
            if (!applied)
            {
                cameraWasEnabled = gameplayCamera.enabled;
                var go = new GameObject("Raccoon editor observer camera");
                observer = go.AddComponent<Camera>();
                observer.CopyFrom(gameplayCamera);
                observer.depth = gameplayCamera.depth + 1;
                observer.nearClipPlane = .05f;
                observer.fieldOfView = 45f;
                RememberShadow(Target.Skin);
                Transform tp = player.transform.Find("TpWeaponHolder");
                if (tp != null) foreach (var r in tp.GetComponentsInChildren<Renderer>(true)) RememberShadow(r);
                Transform fp = player.transform.Find("AimPivot");
                if (fp != null) foreach (var r in fp.GetComponentsInChildren<Renderer>(true)) hidden[r] = r.forceRenderingOff;
                applied = true;
            }
            gameplayCamera.enabled = false;
            foreach (var pair in shadows) if (pair.Key != null) pair.Key.shadowCastingMode = ShadowCastingMode.On;
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = true;
            Vector3 pivot = player.transform.position + Vector3.up * 1.05f;
            Quaternion rotation = Quaternion.Euler(Pitch, player.transform.eulerAngles.y + OrbitYaw, 0f);
            observer.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * Distance, rotation);
        }

        private void RememberShadow(Renderer renderer)
        {
            if (renderer != null) shadows[renderer] = renderer.shadowCastingMode;
        }

        public void Restore()
        {
            if (!applied) return;
            if (gameplayCamera != null) gameplayCamera.enabled = cameraWasEnabled;
            foreach (var pair in shadows) if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            shadows.Clear(); hidden.Clear();
            if (observer != null) Destroy(observer.gameObject);
            observer = null; applied = false;
        }

        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
#endif
