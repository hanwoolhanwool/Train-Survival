using System.IO;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class RaccoonSupportArmSetup
    {
        public static void Build()
        {
            const string output="output/raccoon-support-v17";
            const string path=RaccoonEquipmentSetup.Art+"/RaccoonEquipmentProfile.asset";
            Directory.CreateDirectory(output);
            if(!File.Exists(output+"/profile.before.asset"))File.Copy(path,output+"/profile.before.asset");
            var profile=AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(path);
            profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
            entry.MinimumSupportElbowBend=50;
            entry.Left.ElbowHint=new Vector3(-.35f,.82f,.37f);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            RaccoonLocomotionReview.ReviewV17();
        }
    }
}
