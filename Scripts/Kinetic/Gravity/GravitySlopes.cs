using UnityEngine;

namespace Kinetic.Gravity
{
    public class GravitySlopes : MonoBehaviour
    {
        private GravityOrientationMethod _prevPlayerGravityOrientation = GravityOrientationMethod.None;
        private float _prevPlayerMaxSlopeAngle;
        
        private void OnCollisionEnter(Collision other)
        {
            PlayerCharacterController cc = other.collider.GetComponent<PlayerCharacterController>();
            if (!cc) return;
            
            _prevPlayerGravityOrientation = cc.gravityOrientationMethod;
            _prevPlayerMaxSlopeAngle = cc.motor.MaxStableSlopeAngle;
            cc.motor.MaxStableSlopeAngle = 100;
            cc.gravityOrientationMethod = GravityOrientationMethod.TowardsGroundSlopeAndGravity;
        }
        
        private void OnCollisionExit(Collision other)
        {
            PlayerCharacterController cc = other.collider.GetComponent<PlayerCharacterController>();
            if (!cc) return;
            
            cc.motor.MaxStableSlopeAngle =_prevPlayerMaxSlopeAngle;
            cc.gravityOrientationMethod = _prevPlayerGravityOrientation;
        }
    }
}
