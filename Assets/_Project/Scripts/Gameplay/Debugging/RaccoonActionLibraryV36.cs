using UnityEngine;

namespace Game.Gameplay.Debugging
{
    [CreateAssetMenu(menuName="Game/Raccoon Action Library v36")]
    public sealed class RaccoonActionLibraryV36 : ScriptableObject
    {
        [System.Serializable] public sealed class Entry
        {
            public string Id,Label,Source;
            public AnimationClip Clip;
            public bool Loop,UpperBody,KeepWeapon;
            public string Prop;
            public float Duration;
        }
        public Entry[] Entries;
        public Entry[] ContextPoses;
    }
}
