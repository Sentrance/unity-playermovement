using System;
using KinematicCharacterController;
using Kinetic.Utils;
using UnityEngine;

namespace Kinetic.InvertedKinematics
{
    public class IKFootPlacement : MonoBehaviour
    {
        public KinematicCharacterMotor motor;
        public LayerMask layerMask;
        [Range(0f, 1f)]
        public float distanceToground;
        public GroundChecker groundChecker;
        [Range(100f, 300f)]
        public float SlopeHeightDivider;
        
        private Animator _animator;
        
        private void Start()
        {
            if ((_animator = GetComponent<Animator>()) == null)
                throw new Exception("[IKFootPlacement] No animator found.");
            if (motor == null)
                throw new Exception("[IKFootPlacement] No PlayerCharacterController found.");
        }

        private void OnAnimatorIK(int layerIndex)
        {

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1f);
            
            RaycastHit raycastHit;
            Ray ray = new Ray(_animator.GetIKPosition(AvatarIKGoal.LeftFoot) + motor.CharacterUp, -motor.CharacterUp);
            if (Physics.Raycast(ray, out raycastHit, distanceToground + 1f, layerMask))
            {
                if (motor.GroundingStatus.FoundAnyGround)
                {
                    // Only call this once: allows to change the capsule offset to match ik's foot (the more the slope the lower the player)
                    // TODO: this is very cool and stuff but it prevent from climbing stairs lol. The way out would be to do a lot of raycast to manage many kind of "tricky" slopes (like stairs) but it's tedious
                    // float slopeAngle = groundChecker.groundSlopeAngle;
                    //motor.SetCapsuleDimensions(motor.Capsule.radius, motor.Capsule.height, 0.86f * (1f + slopeAngle / SlopeHeightDivider) );

                    Vector3 footPosition = raycastHit.point;
                    footPosition.y += distanceToground;
                    _animator.SetIKPosition(AvatarIKGoal.LeftFoot, footPosition);
                    _animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.LookRotation(motor.CharacterForward, raycastHit.normal));
                }
            }
            
            ray = new Ray(_animator.GetIKPosition(AvatarIKGoal.RightFoot) + motor.CharacterUp, -motor.CharacterUp);
            if (Physics.Raycast(ray, out raycastHit, distanceToground + 1f, layerMask))
            {
                if (motor.GroundingStatus.FoundAnyGround)
                {
                    Vector3 footPosition = raycastHit.point;
                    footPosition.y += distanceToground;
                    _animator.SetIKPosition(AvatarIKGoal.RightFoot, footPosition);
                    _animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.LookRotation(motor.CharacterForward, raycastHit.normal));
                }
            }
        }
    }
}