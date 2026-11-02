using System.Linq;
using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonDeathDropV49Tests
    {
#if UNITY_EDITOR
        [TestCase(RaccoonActionReviewV36.ReviewContext.Shotgun)]
        [TestCase(RaccoonActionReviewV36.ReviewContext.Hammer)]
        [TestCase(RaccoonActionReviewV36.ReviewContext.Revolver)]
        [TestCase(RaccoonActionReviewV36.ReviewContext.Harpoon)]
        public void WeaponReleaseUsesPreparedSurfacesAndSettlesWithoutMeshReadErrors(RaccoonActionReviewV36.ReviewContext context)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab"));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            try
            {
                var motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;motor.ResetAt(new Vector3(1000,.03f,0));
                var review=root.GetComponent<RaccoonActionReviewV36>();review.SetContext(context);motor.Evaluate(1f/60);
                if(context==RaccoonActionReviewV36.ReviewContext.Shotgun)
                {
                    var filters=review.Weapon.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh.name=="Shotgun_Body_v36"||f.sharedMesh.name=="Shotgun_Pump_v36").ToArray();
                    Assert.That(filters.Length,Is.EqualTo(2));
                    foreach(var filter in filters){Assert.That(filter.sharedMesh.isReadable,Is.False,"Reproduce the user's GPU-only meshes");Assert.That(filter.GetComponent<RaccoonDropSurfaceV49>().Matches(filter.sharedMesh),Is.True);}
                }
                review.Select(System.Array.FindIndex(review.Library.Entries,e=>e.Id==RaccoonDeathCandidateV49.Id));
                review.DeathCandidate.Apply(.17f);Assert.That(review.DeathItemReleased,Is.True);
                var dropped=review.DeathCandidate.Dropped;Assert.That(dropped,Is.Not.Null);
                foreach(float time in new[]{.35f,.9f,2f,4.2f,5f})review.DeathCandidate.Apply(time);
                Assert.That(review.DeathCandidate.Dropped,Is.SameAs(dropped),"Only one copy per release");
                float bottom=float.PositiveInfinity;
                foreach(var filter in dropped.GetComponentsInChildren<MeshFilter>())foreach(var p in RaccoonDropSurfaceV49.ReadSurface(filter))bottom=Mathf.Min(bottom,filter.transform.TransformPoint(p).y);
                Assert.That(bottom,Is.InRange(.005f,.035f));Assert.That(review.DeathCandidate.Settled,Is.True);
                review.ReturnToContext();Assert.That(review.DeathCandidate.Dropped,Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
#endif
        [Test]
        public void UnpreparedUnreadableMeshFallsBackWithoutReadingItsBuffers()
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);var mesh=Object.Instantiate(obj.GetComponent<MeshFilter>().sharedMesh);
            try
            {
                var filter=obj.GetComponent<MeshFilter>();filter.sharedMesh=mesh;mesh.UploadMeshData(true);Assert.That(mesh.isReadable,Is.False);
                var surface=RaccoonDropSurfaceV49.ReadSurface(filter);Assert.That(surface.Length,Is.EqualTo(8));
                Assert.That(surface.Min(p=>p.y),Is.EqualTo(mesh.bounds.min.y));Assert.That(surface.Max(p=>p.y),Is.EqualTo(mesh.bounds.max.y));LogAssert.NoUnexpectedReceived();
            }
            finally{Object.DestroyImmediate(obj);Object.DestroyImmediate(mesh);}
        }
        [Test]
        public void ReplacedMeshDoesNotReuseAnUnrelatedCachedSurface()
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);var filter=obj.GetComponent<MeshFilter>();var original=filter.sharedMesh;var replacement=Object.Instantiate(original);
            try
            {
                obj.AddComponent<RaccoonDropSurfaceV49>().SetSurface(original,new[]{Vector3.one*99});
                replacement.UploadMeshData(true);filter.sharedMesh=replacement;
                var surface=RaccoonDropSurfaceV49.ReadSurface(filter);Assert.That(surface.Length,Is.EqualTo(8));Assert.That(surface.Max(p=>p.magnitude),Is.LessThan(1));LogAssert.NoUnexpectedReceived();
            }
            finally{Object.DestroyImmediate(obj);Object.DestroyImmediate(replacement);}
        }
    }
}
