using System;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>Distributes the support wrist's rotation across three deformation joints.</summary>
    [DefaultExecutionOrder(75)]
    public sealed class RaccoonWristDeformation : MonoBehaviour
    {
        private Transform _forearm, _hand;
        private Quaternion _forearmRest, _handRest;
        private Vector3 _forearmRestPosition, _handRestPosition;
        private Transform[] _blend;
        private Animator _animator;
        private bool _bound;
        public bool PreserveTwistPath {get;set;}
        private bool _hasTwistPath;
        private float _twistAngle;

        public void Bind()
        {
            _bound=false;_hasTwistPath=false;
            _animator=GetComponent<Animator>();
            var skin=GetComponentInChildren<SkinnedMeshRenderer>();
            if(_animator==null || !_animator.isHuman || skin==null)return;
            _forearm=_animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _hand=_animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var bones=skin.bones;var bind=skin.sharedMesh.bindposes;
            int armIndex=Array.IndexOf(bones,_forearm),handIndex=Array.IndexOf(bones,_hand);
            if(armIndex<0 || handIndex<0)return;
            _forearmRest=Quaternion.Inverse(transform.rotation)*(skin.transform.localToWorldMatrix*bind[armIndex].inverse).rotation;
            _handRest=Quaternion.Inverse(transform.rotation)*(skin.transform.localToWorldMatrix*bind[handIndex].inverse).rotation;
            _forearmRestPosition=transform.InverseTransformPoint((skin.transform.localToWorldMatrix*bind[armIndex].inverse).GetColumn(3));
            _handRestPosition=transform.InverseTransformPoint((skin.transform.localToWorldMatrix*bind[handIndex].inverse).GetColumn(3));
            _blend=new Transform[3];
            for(int i=0;i<3;i++)
            {
                _blend[i]=Array.Find(bones,b=>b.name=="LeftWristBlend"+(i+1));
                if(_blend[i]==null)return;
            }
            _bound=true;
        }
        private void Awake()=>Bind();
        private void LateUpdate()=>Apply();
        public void Apply()
        {
            if(!_bound)Bind();
            if(!_bound || !_animator.isActiveAndEnabled)return;
            Quaternion from=_forearm.rotation*Quaternion.Inverse(_forearmRest);
            Quaternion to=_hand.rotation*Quaternion.Inverse(_handRest);
            Quaternion relative=to*Quaternion.Inverse(from);
            Vector3 axis=(_hand.position-_forearm.position).normalized;
            Vector3 projected=axis*Vector3.Dot(new Vector3(relative.x,relative.y,relative.z),axis);
            Quaternion twist=new Quaternion(projected.x,projected.y,projected.z,relative.w);
            float length=Mathf.Sqrt(Quaternion.Dot(twist,twist));
            twist=length>.00001f?new Quaternion(twist.x/length,twist.y/length,twist.z/length,twist.w/length):Quaternion.identity;
            Quaternion swing=relative*Quaternion.Inverse(twist);
            if(PreserveTwistPath)
            {
                // A deliberate half-turn can pass the quaternion shortest-path seam.
                // Keep the deformation joints on the hand's continuous route.
                float angle=2*Mathf.Atan2(Vector3.Dot(new Vector3(twist.x,twist.y,twist.z),axis),twist.w)*Mathf.Rad2Deg;
                angle=Mathf.DeltaAngle(0,angle);
                _twistAngle=_hasTwistPath?_twistAngle+Mathf.DeltaAngle(_twistAngle,angle):angle;
                _hasTwistPath=true;
            }
            else _hasTwistPath=false;
            for(int i=0;i<3;i++)
            {
                float u=(i+1)/4f;
                Quaternion rotation=Quaternion.Slerp(Quaternion.identity,swing,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1,u)))
                    *(PreserveTwistPath?Quaternion.AngleAxis(_twistAngle*u,axis):Quaternion.Slerp(Quaternion.identity,twist,u))*from;
                Vector3 center=Vector3.Lerp(_forearm.position,_hand.position,u);
                Vector3 restOffset=_handRestPosition-Vector3.Lerp(_forearmRestPosition,_handRestPosition,u);
                // The bind pivot is the wrist; compensate its translation to keep each
                // forearm section on its own centerline while distributing pronation.
                Vector3 position=center+rotation*Vector3.Scale(restOffset,transform.lossyScale);
                _blend[i].SetPositionAndRotation(position,rotation);
            }
        }
    }
}
