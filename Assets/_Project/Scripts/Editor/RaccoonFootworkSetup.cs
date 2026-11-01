using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    public static class RaccoonFootworkSetup
    {
        public static void Build()
        {
            const string output="output/raccoon-footwork-v21";
            string path=RaccoonEquipmentSetup.Art+"/AC_Raccoon_Base_v7.controller";
            Directory.CreateDirectory(output);
            if(!File.Exists(output+"/controller.before.asset"))File.Copy(path,output+"/controller.before.asset");
            var tree=AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().First(t=>t.name=="Locomotion");
            var children=tree.children;
            for(int i=0;i<children.Length;i++)
            {
                if(Mathf.Approximately(children[i].threshold,4.5f))children[i].timeScale=3.5f;
                else if(Mathf.Approximately(children[i].threshold,7f))
                {
                    children[i].timeScale=1f;
                    if(children[i].motion is BlendTree run)
                    {
                        var variants=run.children;
                        for(int j=0;j<variants.Length;j++)variants[j].timeScale=2.2f;
                        run.children=variants;EditorUtility.SetDirty(run);
                    }
                }
            }
            tree.children=children;EditorUtility.SetDirty(tree);AssetDatabase.SaveAssets();
            RaccoonLocomotionReview.CaptureV21();
        }
    }
}
