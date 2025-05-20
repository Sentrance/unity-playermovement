using System;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kinetic
{
    /// <summary>
    /// This class is the core controller for the character and uses the interface provided by "KinematicCharacterController".
    /// It defines states and the transition between them.
    /// The behavior of the PlayerCharacterController changes based on the current state it's in.
    /// </summary>
    public class PlayerCharacterController : MonoBehaviour, ICharacterController
    {
        public struct PlayerCharacterInputs
        {
            public float MoveAxisForward;
            public float MoveAxisRight;
            public Quaternion CameraRotation;
            public bool JumpDown;
            public bool JumpHeld;
            public bool SprintDown;
            public bool CrouchHeld;
        }
        
        public KinematicCharacterMotor motor;

        [Header("Debug values")]
        public float dbgCurrentSpeed;
        
        [Header("Stable Movement")]
        public float stableMoveSpeed = 10f;
        public float stableMovementSharpness = 15;
        public float orientationSharpness = 10;
        public OrientationMethod orientationMethod = OrientationMethod.TowardsCamera;

        [Header("Sprint")]
        [Range(1f, 2f)]
        public float sprintMultiplier = 1.20f;

        [Header("Air Movement")]
        public float airMoveSpeed = 10f;
        public float airAccelerationSpeed = 5f;
        public float drag = 0.1f;
        
        [Header("Jumping")]
        public bool allowJumpingWhenSliding = false;
        public float jumpSpeed = 10f;
        public float jumpPreGroundingGraceTime = 0f;
        public float jumpPostGroundingGraceTime = 0f;
        
        [Header("Swimming")]
        public Transform swimmingReferencePoint;
        public LayerMask waterLayer;
        public float swimmingSpeed = 4f;
        public float swimmingMovementSharpness = 3;
        public float swimmingOrientationSharpness = 2f;

        [Header("Falling")]
        public float hardLandRequiredVelocity = 15f;
        public bool freezeOnHardLand;
        public float freezeTime;

        [Header("Gravity")]
        public Vector3 gravity = new Vector3(0, -30f, 0);
        public GravityOrientationMethod gravityOrientationMethod = GravityOrientationMethod.None;
        public float bonusOrientationSharpness = 10f;

        [Header("Crouching")]
        public Transform meshRoot;
        public float crouchSlowingMultiplicator = 1f;
        public float CrouchedCapsuleHeight = 1f;
        public float CrouchedCapsuleRadius = 0.16f;
        public float CrouchCapsuleYOffset = 0.86f;

        // TODO
        [Header("Slopes")]
        public float downSlopeIncreasingVelocity = 1f;
        public float downSlopeVelocityRetainingTime = 1f;
        public float upSlopeDecreasingVelocity = 1f;
        
        public CharacterState CurrentCharacterState { get; private set; }

        // Control vectors
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        
        // Jumping related
        private bool _jumpInputIsHeld;
        private bool _jumpRequested;
        private bool _jumpConsumed;
        private bool _jumpedThisFrame;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump;
        
        // Swimming related
        private Collider[] _waterOverlappedColliders = new Collider[8];
        private Collider _waterZone;
        
        // Crouching related
        private Collider[] _probedColliders = new Collider[8];
        private bool _crouchInputIsHeld;
        private bool _shouldBeCrouching;
        private float _prevCapsuleHeight;
        private float _prevCapsuleRadius;
        private float _prevCapsuleYOffset;
        public bool IsCrouching { get; private set; }
        
        // Sprint related
        private bool _sprintInputIsHeld;
        private float _groundSpeedWithSprint;
        private float _airSpeedWithSprint;
        
        // Landing related
        public bool IsFallingHard { get; private set; }
        public float NoGroundTime { get; private set; }
        private float _prevVelocityY;
        
        // Slope speed related
        public bool IsSlopeSprinting { get; private set; }
        
        // Other
        private Vector3 _internalVelocityAdd = Vector3.zero;

        #region General
        
        public void Start()
        {
            motor.CharacterController = this;
            _groundSpeedWithSprint = stableMoveSpeed;
            _airSpeedWithSprint = airMoveSpeed;
            
            // Get capsule infos since we'll modify them later on crouch
            _prevCapsuleHeight = motor.Capsule.height;
            _prevCapsuleRadius = motor.Capsule.radius;
            _prevCapsuleYOffset = motor.Capsule.center.y;

            TransitionToState(CharacterState.Default);
        }
        
        public void AddVelocity(Vector3 velocity)
        {
            _internalVelocityAdd += velocity;
        }
        
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    DefaultStateVelocity(ref currentVelocity, deltaTime);
                    break;
                case CharacterState.Swimming:
                    SwimmingStateVelocity(ref currentVelocity, deltaTime);
                    break;
                case CharacterState.Falling:
                    FallingStateVelocity(ref currentVelocity, deltaTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Take into account additive velocity
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }

            dbgCurrentSpeed = currentVelocity.magnitude;
        }
        
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Falling:
                case CharacterState.Swimming:
                    DefaultStateRotation(ref currentRotation, deltaTime);
                    break;
                default:
                    throw new Exception("[PlayerCharacterController] Accessed default switch : shouldn't be possible.");
            }
        }

        public void SetInputs(ref PlayerCharacterInputs inputs, float deltaTime)
        {
            _jumpInputIsHeld = inputs.JumpHeld;
            _crouchInputIsHeld = inputs.CrouchHeld;
            _sprintInputIsHeld = inputs.SprintDown;

            // Clamp input
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

            // Calculate camera direction and rotation on the character plane
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, motor.CharacterUp);

            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    DefaultStateInputs(ref inputs, moveInputVector, cameraPlanarDirection, cameraPlanarRotation, deltaTime);
                    break;
                case CharacterState.Swimming:
                    SwimmingStateInputs(ref inputs, moveInputVector, cameraPlanarDirection, deltaTime);
                    break;
                case CharacterState.Falling:
                    FallingStateInputs(moveInputVector, cameraPlanarDirection, cameraPlanarRotation, deltaTime);
                    break;
                default:
                    throw new Exception("[PlayerCharacterController] Accessed default switch : shouldn't be possible.");
            }
        }
        
        public void BeforeCharacterUpdate(float deltaTime)
        {
            // Swimming: do a character overlap test to detect water surfaces
            if (motor.CharacterOverlap(motor.TransientPosition, motor.TransientRotation, _waterOverlappedColliders,
                waterLayer, QueryTriggerInteraction.Collide) > 0)
            {
                // Return if no water surface is detected
                if (_waterOverlappedColliders[0] == null) return;

                // Transition to swimming state if "SwimRefPoint" touches water or to default state if it doesn't
                if (Physics.ClosestPoint(swimmingReferencePoint.position, _waterOverlappedColliders[0],
                        _waterOverlappedColliders[0].transform.position,
                        _waterOverlappedColliders[0].transform.rotation) == swimmingReferencePoint.position)
                {
                    if (CurrentCharacterState == CharacterState.Swimming) return;

                    TransitionToState(CharacterState.Swimming);
                    _waterZone = _waterOverlappedColliders[0];
                }
                else 
                    if (CurrentCharacterState == CharacterState.Swimming) 
                        TransitionToState(CharacterState.Default);
            }

            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Swimming:
                    break;
                case CharacterState.Falling:
                    BeforeFallingUpdate();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    AfterUpdateJump(deltaTime);
                    AfterUpdateCrouch();
                    break;
                case CharacterState.Swimming:
                    break;
                case CharacterState.Falling:
                    AfterUpdateFall(deltaTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Falling can come out of any states
            if (!motor.GroundingStatus.IsStableOnGround && CurrentCharacterState != CharacterState.Swimming) 
                TransitionToState(CharacterState.Falling);
        }
        
        
        #endregion

        /*
         * DEFAULT STATE
         */

        #region Default state
        
        private void DefaultStateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 targetMovementVelocity;
            if (motor.GroundingStatus.IsStableOnGround)
            {
                // TODO: doesn't look like it does anything? I commented it because I believe unknown code behavior could cause unknown issues.
                // Reorient source velocity on current ground slope (this is because we don't want our smoothing to cause any velocity losses in slope changes)
                // currentVelocity = motor.GetDirectionTangentToSurface(currentVelocity, motor.GroundingStatus.GroundNormal) 
                //                   * currentVelocity.magnitude;

                // Calculate target velocity
                Vector3 inputRight = Vector3.Cross(_moveInputVector, motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(motor.GroundingStatus.GroundNormal, inputRight)
                    .normalized * _moveInputVector.magnitude;
                targetMovementVelocity = reorientedInput * _groundSpeedWithSprint;
                
                // Slow down Player on crouch
                if (IsCrouching)
                    targetMovementVelocity *= crouchSlowingMultiplicator;

                // Smooth movement Velocity
                currentVelocity = Vector3.Lerp(
                    currentVelocity, 
                    targetMovementVelocity, 
                    1 - Mathf.Exp(-stableMovementSharpness * deltaTime));
            }
            else
            {
                // Add move input
                if (_moveInputVector.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = _moveInputVector * _airSpeedWithSprint;

                    // Prevent climbing on un-stable slopes with air movement
                    if (motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpendicularObstructionNormal = Vector3.Cross(
                            Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal), 
                            motor.CharacterUp)
                            .normalized;
                        targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpendicularObstructionNormal);
                    }

                    Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, gravity);
                    currentVelocity += velocityDiff * (airAccelerationSpeed * deltaTime);
                }

                // Gravity
                currentVelocity += gravity * deltaTime;

                // Drag
                currentVelocity *= (1f / (1f + (drag * deltaTime)));
            }
            
            // Handle jumping
            _jumpedThisFrame = false;
            _timeSinceJumpRequested += deltaTime;
            if (_jumpRequested)
            {
                // See if we actually are allowed to jump
                if (_jumpConsumed ||
                    (allowJumpingWhenSliding ? !motor.GroundingStatus.FoundAnyGround : !motor.GroundingStatus.IsStableOnGround)
                     && !(_timeSinceLastAbleToJump <= jumpPostGroundingGraceTime)) return;

                // Calculate jump direction before ungrounding
                Vector3 jumpDirection = motor.CharacterUp;
                if (motor.GroundingStatus.FoundAnyGround && !motor.GroundingStatus.IsStableOnGround)
                {
                    jumpDirection = motor.GroundingStatus.GroundNormal;
                }

                // Makes the character skip ground probing/snapping on its next update. 
                // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                motor.ForceUnground(0.1f);

                // Add to the return velocity and reset jump state
                currentVelocity += (jumpDirection * jumpSpeed) - Vector3.Project(currentVelocity, motor.CharacterUp);
                _jumpRequested = false;
                _jumpConsumed = true;
                _jumpedThisFrame = true;
            }
        }

        private void DefaultStateRotation(ref Quaternion currentRotation, float deltaTime)
        { 
            if (_lookInputVector != Vector3.zero && orientationSharpness > 0f)
            {
                // Smoothly interpolate from current to target look direction
                Vector3 smoothedLookInputDirection = Vector3.Slerp(
                    motor.CharacterForward, 
                    _lookInputVector, 
                    1 - Mathf.Exp(-orientationSharpness * deltaTime)
                    ).normalized;

                // Set the current rotation (which will be used by the KinematicCharacterMotor)
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
            }

            Vector3 currentUp = (currentRotation * Vector3.up);
            if (gravityOrientationMethod == GravityOrientationMethod.TowardsGravity)
            {
                // Rotate from current up to invert gravity
                Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -gravity.normalized, 
                    1 - Mathf.Exp(-bonusOrientationSharpness * deltaTime));
                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
            }
            else if (gravityOrientationMethod == GravityOrientationMethod.TowardsGroundSlopeAndGravity)
            {
                if (motor.GroundingStatus.IsStableOnGround)
                {
                    Vector3 intialCharacterBottomCenter = motor.TransientPosition + (currentUp * motor.Capsule.radius);
                    Vector3 smoothedGroundNormal = Vector3.Slerp(motor.CharacterUp,
                        motor.GroundingStatus.GroundNormal, 1 - Mathf.Exp(-bonusOrientationSharpness * deltaTime));
                    currentRotation = Quaternion.FromToRotation(currentUp, smoothedGroundNormal) * currentRotation;

                    // Move the position to create a rotation around the bottom hemi center instead of around the pivot
                    motor.SetTransientPosition(intialCharacterBottomCenter +
                                               (currentRotation * Vector3.down * motor.Capsule.radius));
                }
                else
                {
                    // Rotate from current up to invert gravity
                    Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -gravity.normalized, 
                        1 - Mathf.Exp(-bonusOrientationSharpness * deltaTime));
                    currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                }
            }
            else
            {
                Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, Vector3.up,
                    1 - Mathf.Exp(-bonusOrientationSharpness * deltaTime));
                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
            }
        }

        private void DefaultStateInputs(ref PlayerCharacterInputs inputs, Vector3 moveInputVector,
            Vector3 cameraPlanarDirection, Quaternion cameraPlanarRotation, float deltaTime)
        {
            // Move and look inputs
            if (!IsFrozen(deltaTime))
                _moveInputVector = cameraPlanarRotation * moveInputVector;
            switch (orientationMethod)
            {
                case OrientationMethod.TowardsCamera:
                    _lookInputVector = cameraPlanarDirection;
                    break;
                case OrientationMethod.TowardsMovement:
                    _lookInputVector = _moveInputVector.normalized;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            // Jumping input
            if (inputs.JumpDown)
            {
                _timeSinceJumpRequested = 0f;
                _jumpRequested = true;
            }
            
            // Crouching input
            if (inputs.CrouchHeld)
            {
                _shouldBeCrouching = true;

                // Return if player is already crouching
                if (IsCrouching) return;
                IsCrouching = true;
                motor.SetCapsuleDimensions(CrouchedCapsuleRadius, CrouchedCapsuleHeight, CrouchCapsuleYOffset);
            }
            else 
                _shouldBeCrouching = false; 
            
            // Can we run & do we run check
            _groundSpeedWithSprint = (inputs.SprintDown && !inputs.CrouchHeld) ?
                stableMoveSpeed * sprintMultiplier : stableMoveSpeed;                
        }

        private void AfterUpdateJump(float deltaTime)
        {
            // Handle jumping pre-ground grace period
            if (_jumpRequested && _timeSinceJumpRequested > jumpPreGroundingGraceTime) 
                _jumpRequested = false;
            
            // Handle jumping while sliding
            if (allowJumpingWhenSliding ? motor.GroundingStatus.FoundAnyGround : motor.GroundingStatus.IsStableOnGround)
            {
                // If we're on a ground surface, reset jumping values
                if (!_jumpedThisFrame)
                {
                    _jumpConsumed = false;
                }
                _timeSinceLastAbleToJump = 0f;
            }
            else
            {
                // Keep track of time since we were last able to jump (for grace period)
                _timeSinceLastAbleToJump += deltaTime;
            }
        }

        private void AfterUpdateCrouch()
        {
            if (!IsCrouching || _shouldBeCrouching) return;

            // Do an overlap test with the character's standing height to see if there are any obstructions
            motor.SetCapsuleDimensions(_prevCapsuleRadius, _prevCapsuleHeight, _prevCapsuleYOffset);
            if (motor.CharacterOverlap(
                    motor.TransientPosition,
                    motor.TransientRotation,
                    _probedColliders,
                    motor.CollidableLayers,
                    QueryTriggerInteraction.Ignore) > 0)
            {
                // If obstructions, just stick to crouching dimensions
                motor.SetCapsuleDimensions(CrouchedCapsuleRadius, CrouchedCapsuleHeight, CrouchCapsuleYOffset);
            }
            else
            {
                // If no obstructions, uncrouch
                IsCrouching = false;
            }
        }
        
        #endregion
        
        /*
         * FALLING STATE
         */

        #region Falling state

        private void BeforeFallingUpdate()
        {
            IsFallingHard = Math.Abs(_prevVelocityY) >= hardLandRequiredVelocity && Math.Abs(motor.Velocity.y) <= 0.5;
            _prevVelocityY = motor.Velocity.y;
        }

        private void AfterUpdateFall(float deltaTime)
        {
            if (motor.GroundingStatus.IsStableOnGround)
            {
                NoGroundTime = 0f;
                TransitionToState(CharacterState.Default);
                return;
            }
            
            NoGroundTime += deltaTime;
        }

        private void FallingStateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // Add move input
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 targetMovementVelocity;
                targetMovementVelocity = _moveInputVector * _airSpeedWithSprint;

                // Prevent climbing on un-stable slopes with air movement
                if (motor.GroundingStatus.FoundAnyGround)
                {
                    Vector3 perpendicularObstructionNormal = Vector3.Cross(
                            Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal), 
                            motor.CharacterUp).normalized;
                    targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpendicularObstructionNormal);
                }

                Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, gravity);
                currentVelocity += velocityDiff * (airAccelerationSpeed * deltaTime);
            }

            // Gravity
            currentVelocity += gravity * deltaTime;

            // Drag
            currentVelocity *= (1f / (1f + (drag * deltaTime)));
        }

        private void FallingStateInputs(Vector3 moveInputVector,
            Vector3 cameraPlanarDirection, Quaternion cameraPlanarRotation, float deltaTime)
        {
            // Look vector
            switch (orientationMethod)
            {
                case OrientationMethod.TowardsCamera:
                    _lookInputVector = cameraPlanarDirection;
                    break;
                case OrientationMethod.TowardsMovement:
                    _lookInputVector = _moveInputVector.normalized;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            // Move vector
            if (!IsFrozen(deltaTime)) 
                _moveInputVector = cameraPlanarRotation * moveInputVector;
        }
        
        private bool IsFrozen(float deltaTime)
        {
            // Stop move input if player is on freezing hard fall state
            if (freezeTime > 0)
            {
                freezeTime -= deltaTime;
                return freezeOnHardLand;
            }

            freezeTime = 0f;
            return false;
        }

        #endregion

        /*
         * Swimming State
         */

        #region Swimming state

        private void SwimmingStateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            float verticalInput = 0f + (_jumpInputIsHeld ? 1f : 0f) + (_crouchInputIsHeld ? -1f : 0f);

            // Smoothly interpolate to target swimming velocity
            Vector3 targetMovementVelocity =
                (_moveInputVector + (motor.CharacterUp * verticalInput)).normalized * swimmingSpeed;
            Vector3 smoothedVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity,
                1 - Mathf.Exp(-swimmingMovementSharpness * deltaTime));

            // See if our swimming reference point would be out of water after the movement from our velocity has been applied
            {
                Vector3 resultingSwimmingReferencePosition =
                    motor.TransientPosition + (smoothedVelocity * deltaTime) +
                    (swimmingReferencePoint.position - motor.TransientPosition);
                Vector3 closestPointWaterSurface = Physics.ClosestPoint(resultingSwimmingReferencePosition, _waterZone,
                    _waterZone.transform.position, _waterZone.transform.rotation);

                // if our position would be outside the water surface on next update, project the velocity on the surface normal so that it would not take us out of the water
                // TODO: Rework this system so it doesn't prevent leaving water on sides, ex: waterfall or water cube
                if (closestPointWaterSurface != resultingSwimmingReferencePosition)
                {
                    Vector3 waterSurfaceNormal =
                        (resultingSwimmingReferencePosition - closestPointWaterSurface).normalized;
                    smoothedVelocity = Vector3.ProjectOnPlane(smoothedVelocity, waterSurfaceNormal);

                    // Jump out of water
                    if (_jumpRequested)
                    {
                        smoothedVelocity += (motor.CharacterUp * jumpSpeed) -
                                            Vector3.Project(currentVelocity, motor.CharacterUp);
                    }
                }
                
                currentVelocity = smoothedVelocity;
            }
        }

        private void SwimmingStateInputs(ref PlayerCharacterInputs inputs, Vector3 moveInputVector,
            Vector3 cameraPlanarDirection, float deltaTime)
        {
            _jumpRequested = inputs.JumpHeld;
            if (!IsFrozen(deltaTime)) 
                _moveInputVector = inputs.CameraRotation * moveInputVector;

            switch (orientationMethod)
            {
                case OrientationMethod.TowardsCamera:
                    _lookInputVector = cameraPlanarDirection;
                    break;
                case OrientationMethod.TowardsMovement:
                    _lookInputVector = _moveInputVector.normalized;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion

        /*
         * STATES MANAGEMENT
         */

        #region State management
        
        public void TransitionToState(CharacterState newState)
        {
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        private void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    motor.SetGroundSolvingActivation(true);
                    break;
                case CharacterState.Swimming:
                    IsCrouching = false;
                    motor.SetGroundSolvingActivation(false);
                    break;
                case CharacterState.Falling:
                    motor.SetGroundSolvingActivation(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void OnStateExit(CharacterState state, CharacterState toState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    break;
                case CharacterState.Swimming:
                    break;
                case CharacterState.Falling:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
        
        #endregion

        /*
         * NOT IMPLEMENTED METHODS
         */
        
        public bool IsColliderValidForCollisions(Collider coll)
        {
            // throw new System.NotImplementedException();
            return true;
        }
        
        public void PostGroundingUpdate(float deltaTime)
        {
            // throw new System.NotImplementedException();
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            // throw new System.NotImplementedException();
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
            // throw new System.NotImplementedException();
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition,
            Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            // throw new System.NotImplementedException();
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            // throw new System.NotImplementedException();
        }
    }
}
