using System;
using System.IO;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class RaccoonGripContactSetup
    {
        public static void BuildThumbV20()
        {
            const string output = "output/raccoon-thumb-v20";
            string path = RaccoonEquipmentSetup.Art + "/RaccoonEquipmentProfile.asset";
            if (!File.Exists(output + "/profile.before.asset"))
                File.Copy(path, output + "/profile.before.asset");
            var data = JsonUtility.FromJson<ContactPose>(File.ReadAllText(output + "/contact_pose.json"));
            var profile = AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(path);
            profile.TryGetEntry(HotbarItemType.Shotgun, out var entry);
            // Thumb only: preserve all other fingers, hand targets and the arm stance.
            Array.Copy(data.fingers, 0, entry.Left.Fingers, 0, 3);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            RaccoonLocomotionReview.CaptureV20();
        }

        [Serializable] private class ContactPose { public Vector3 leftPosition;public Vector3 rightPosition;public float bodyYaw;public float minimumBend;public Quaternion[] fingers; }
        public static void Build()
        {
            const string output="output/raccoon-grip-v19";
            string path=RaccoonEquipmentSetup.Art+"/RaccoonEquipmentProfile.asset";
            if(!File.Exists(output+"/profile.before.asset"))File.Copy(path,output+"/profile.before.asset");
            var data=JsonUtility.FromJson<ContactPose>(File.ReadAllText(output+"/contact_pose.json"));
            var profile=AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(path);
            profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
            entry.Left.Position=data.leftPosition;entry.Right.Position=data.rightPosition;profile.TwoHandBodyYaw=data.bodyYaw;
            entry.Left.Fingers=data.fingers;entry.MinimumSupportElbowBend=data.minimumBend;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            RaccoonLocomotionReview.CaptureV19();
        }
    }
}
