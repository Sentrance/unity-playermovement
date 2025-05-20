using System;
using System.Linq;
using KinematicCharacterController.Examples;
using KinematicCharacterController.Walkthrough.PlayerCameraCharacterSetup;
using Kinetic.Camera;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kinetic
{
    /// <summary>
    /// This class handles the link between the PlayerCharacterController, the inputs, the camera and the animator variables.
    /// </summary>
    public class Player : MonoBehaviour
    {
        [Header("Player")]
        public PlayerCharacterController character;
        public PlayerInput playerInput;

        [Header("Camera")]
        public CharacterCamera orbitCamera;
        public Transform cameraFollowPoint;

        [Header("Animation references")]
        public Animator animator;
        public string isMovingReference = "isMoving";
        public string movementSpeedReference = "movementSpeed";
        public string isGroundedReference = "isGrounded";

        public string isSwimmingReference = "isSwimming";

        public string isJumping = "isJumping";
        
        public string isCrouchingReference = "isCrouching";

        public string isFallingReference = "isFalling";
        public string isHardLanding = "isHardLanding";
        

        [Header("Input references")]
        public string playerMovementReferenceName = "Movement";
        public string cameraMovementReferenceName = "WheelMovement";
        public string cameraRecenterReferenceName = "CameraRecenter";
        public string scrollReferenceName = "WheelScroll";
        public string sprintReferenceName = "Sprint";
        public string jumpReferenceName = "Jump";
        public string crouchReferenceName = "Crouch";

        [Header("Misc")]
        public int underwaterFollowingSharpness = 10;
        public int underwaterRotationSharpness = 4;
        [Range(0, 1)]
        public float ConsiderFallingTimeInSeconds = 0.3f;


        private Vector3 _lookInputVector = Vector3.zero;
        private bool _jumpConsumed;
        
        private void Awake()
        {
            if (playerInput == null)
                throw new Exception("[Player] No player input linked in script!");
            if (orbitCamera == null)
                throw new Exception("[Player] No orbit camera input linked in script!");
            if (cameraFollowPoint == null)
                throw new Exception("[Player] No camera follow point linked in script!");
            if (character == null)
                throw new Exception("[Player] No character linked in script!");
            if (animator == null)
                throw new Exception("[Player] No animator linked in script!");

            Cursor.lockState = CursorLockMode.Locked;
            
            // Recenter camera
            playerInput.actions[cameraRecenterReferenceName].performed += ctx => CameraRecenter();
            
            // Tell camera to follow transform
            orbitCamera.SetFollowTransform(cameraFollowPoint);

            // Ignore the character's collider(s) for camera obstruction checks
            orbitCamera.IgnoredColliders.Clear();
            orbitCamera.IgnoredColliders.AddRange(character.GetComponentsInChildren<Collider>());
        }
        
        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            // Tell camera to follow transform
            orbitCamera.SetFollowTransform(cameraFollowPoint);

            // Ignore the character's collider(s) for camera obstruction checks
            orbitCamera.IgnoredColliders = character.GetComponentsInChildren<Collider>().ToList();
        }

        private void Update()
        {
            HandleMovement(playerInput.actions[playerMovementReferenceName].ReadValue<Vector2>());
        }

        private void LateUpdate()
        {
            HandleCameraInput(playerInput.actions[cameraMovementReferenceName].ReadValue<Vector2>(), 
                playerInput.actions[scrollReferenceName].ReadValue<float>());
        }

        private void CameraLock()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void CameraRecenter()
        {
            orbitCamera.ResetRotation(character.transform.forward);
        }

        private void HandleMovement(Vector2 playerMovement)
        {
            PlayerCharacterController.PlayerCharacterInputs characterInputs = new PlayerCharacterController.PlayerCharacterInputs();

            characterInputs.MoveAxisForward = playerMovement.y;
            characterInputs.MoveAxisRight = playerMovement.x;
            characterInputs.CameraRotation = orbitCamera.Transform.rotation;
            characterInputs.JumpHeld = playerInput.actions[jumpReferenceName].IsPressed();
            characterInputs.SprintDown = playerInput.actions[sprintReferenceName].IsPressed();
            characterInputs.CrouchHeld = playerInput.actions[crouchReferenceName].IsPressed();
            
            // Manage jump
            characterInputs.JumpDown = playerInput.actions[jumpReferenceName].ReadValue<float>() > 0;

            
            // Apply inputs to character
            character.SetInputs(ref characterInputs, Time.deltaTime);
            
            // Animate the character
            ApplyAnimationReferences(characterInputs);
        }

        private void HandleCameraInput(Vector2 cameraMovement, float wheelScroll = 0)
        {
            // Create the look input vector for the camera
            float mouseLookAxisUp = cameraMovement.y;
            float mouseLookAxisRight = cameraMovement.x;

            _lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);

            // Prevent moving the camera while the cursor isn't locked
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                _lookInputVector = Vector3.zero;
            }

            // Input for zooming the camera (disabled in WebGL because it can cause problems)
            float scrollInput = -wheelScroll;

#if UNITY_WEBGL
            scrollInput = 0f;
#endif
            
            // Apply inputs to the camera based on character's state
            switch (character.CurrentCharacterState)
            {
                case CharacterState.Default: 
                case CharacterState.Falling:
                    orbitCamera.UpdateWithInput(Time.deltaTime, scrollInput, _lookInputVector, true);
                    break;
                case CharacterState.Swimming:
                    orbitCamera.UpdateWithInput(Time.deltaTime, scrollInput, _lookInputVector, false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ApplyAnimationReferences(PlayerCharacterController.PlayerCharacterInputs characterInputs)
        { 
            // TODO: manage how to freeze player if needed. It works by when to call it ? How can we stop other animations ? 
            // I don't even understand the above statement anymore, must have been drunk writing it. But I think it should be done in thePlayerCharacterController anyway.
            animator.SetBool(isMovingReference,
                Math.Abs(characterInputs.MoveAxisForward) > 0 || Math.Abs(characterInputs.MoveAxisRight) > 0);
            animator.SetFloat(movementSpeedReference, character.motor.Velocity.magnitude);
            
            // Swimming
            animator.SetBool(isSwimmingReference, character.CurrentCharacterState == CharacterState.Swimming);
            
            // Jumping
            if (character.CurrentCharacterState == CharacterState.Default && character.motor.GroundingStatus.IsStableOnGround)
                animator.SetBool(isJumping, characterInputs.JumpDown);
            else
                animator.SetBool(isJumping, false);
            
            animator.SetBool(isGroundedReference, character.motor.GroundingStatus.IsStableOnGround);

            // Crouching
            animator.SetBool(isCrouchingReference, character.IsCrouching);

            // Falling
            animator.SetBool(isHardLanding, character.IsFallingHard);
        }
    }
}