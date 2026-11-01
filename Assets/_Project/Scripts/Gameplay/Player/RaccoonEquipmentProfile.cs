using System;
using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Game/Raccoon Equipment Profile")]
    public sealed class RaccoonEquipmentProfile : ScriptableObject
    {
        [Serializable]
        public sealed class HandPose
        {
            public Vector3 Position;
            public Vector3 ElbowHint;
            public Quaternion Rotation = Quaternion.identity;
            public Quaternion[] Fingers = new Quaternion[15];
        }

        [Serializable]
        public sealed class SocketPose
        {
            public Vector3 Position;
            public Quaternion Rotation = Quaternion.identity;
            public Vector3 Scale = Vector3.one;
        }

        [Serializable]
        public sealed class Entry
        {
            public HotbarItemType Item;
            public HandPose Right = new HandPose();
            public HandPose Left = new HandPose();
            public bool TwoHanded;
            [Range(0,100)] public float MinimumSupportElbowBend;
            public SocketPose Socket = new SocketPose();
            public Mesh MeshOverride;
        }

        public Vector3 RightShoulder;
        public Vector3 LeftShoulder;
        public Entry[] Entries = Array.Empty<Entry>();
        public HandPose LeftHarpoon = new HandPose();
        public SocketPose LeftHarpoonSocket = new SocketPose();
        [Min(.01f)] public float BlendSeconds = .16f;
        [Range(0,1)] public float ChestPitchWeight = .25f;
        [Range(0,1)] public float HeadPitchWeight = .5f;
        public float HeadNeutralPitch = -16f;
        [Range(0,1)] public float HandPositionPitchWeight = .35f;
        public float MinimumGripForward = .46f;
        public float TwoHandBodyYaw = 26f;
        public bool HasAuthoredShotgunRun;
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int RunStyleId = Animator.StringToHash("RaccoonShotgunRun");

        public float AuthoredRunWeight(Animator animator)
        {
            if(!HasAuthoredShotgunRun || animator==null || animator.runtimeAnimatorController==null
                || !animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"))return 0;
            return Mathf.InverseLerp(4.5f,7f,animator.GetFloat(SpeedId))*Mathf.Clamp01(animator.GetFloat(RunStyleId));
        }

        public float HeadPitchFor(Animator animator)=>Mathf.Lerp(HeadNeutralPitch,0,AuthoredRunWeight(animator));

        public bool TryGetEntry(HotbarItemType item, out Entry entry)
        {
            foreach (Entry candidate in Entries)
                if (candidate != null && candidate.Item == item) { entry = candidate; return true; }
            entry = null;
            return false;
        }
    }
}
