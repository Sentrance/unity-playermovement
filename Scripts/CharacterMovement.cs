using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = System.Object;

// Todo: I think this is an old script that isn't used anymore. Should we script it?
public class CharacterMovement : MonoBehaviour
{
    public Animator animator;
    public string isSprintingReference = "isSprinting";
    public string movementSpeedReference = "movementSpeed";

    public PlayerInput playerInput;
    public string movementReferenceName = "Movement";
    public string sprintReference = "Sprint";

    public float playerSpeed = 8f;
    public float rotationSpeed = 720f;
    // public float gravity = 0.5f;

    public bool allowStairClimb = true;
    public Transform stepRaycastLower;
    public Transform stepRaycastUpper;
    public float maxStepHeight = 0.4f;
    public float stepSmooth = 0.6f;
    // Change this value to detect stairs further or closer
    public float stairMaxDistance = 0.2f;
    // Value to prevent climbing on a "stair" next to a wall
    public float stairDeepness = 0.3f;

    private Rigidbody _rigidbody;
    private int _isWalkingHash = -1;
    private int _isRunningHash = -1;

    private Vector3 _upAxis;

    private void Awake()
    {
        if (animator == null)
            throw new Exception("[CharacterMovement] No animator linked in script!");

        if (playerInput == null)
            throw new Exception("[CharacterMovement] No player input linked in script!");

        if ((_rigidbody = GetComponent<Rigidbody>()) == null)
            throw new Exception("[CharacterMovement] No Rigidbody found in object!");
        SetAnimationHashReferences();
        _rigidbody.useGravity = false;
        
        playerInput.actions[movementReferenceName].performed += ctx => HandleMovement(ctx.ReadValue<Vector2>());
        playerInput.actions[sprintReference].performed += ctx => Debug.Log(ctx.ReadValueAsObject());
    }

    private void OnEnable()
    {
        playerInput.actions.Enable();
    }

    private void OnDisable()
    {
        playerInput.actions.Disable();
    }

    // Start is called before the first frame update
    void Start()
    {
        // Set RayCast position at maxStepHeight
        var bufferPosition = stepRaycastUpper.position;
        bufferPosition = new Vector3(bufferPosition.x, bufferPosition.y + maxStepHeight, bufferPosition.z);
        stepRaycastUpper.position = bufferPosition;
    }

    private void SetAnimationHashReferences()
    {
        _isWalkingHash = Animator.StringToHash(isSprintingReference);
        if (_isWalkingHash == -1)
            throw new Exception("[CharacterMovement] No walking variable in animator!");

        _isRunningHash = Animator.StringToHash(movementSpeedReference);
        if (_isRunningHash == -1)
            throw new Exception("[CharacterMovement] No running variable in animator!");
    }

    private void FixedUpdate()
    {
        _rigidbody.velocity += CustomGravity.GetGravity(_rigidbody.position, out _upAxis) * Time.deltaTime;
    }

    private void HandleMovement(Vector2 movement)
    {
        // Converts normalized input into movement intensity reference for animator
        float tmpX = (movement.x < 0)? movement.x * -1: movement.x;
        float tmpY = (movement.y < 0)? movement.y * -1: movement.y;
        animator.SetFloat(movementSpeedReference, (tmpX > tmpY)? tmpX : tmpY);

        if (movement == Vector2.zero)
            return;

        if (allowStairClimb)
            StepClimb();
        
        Vector3 direction = new Vector3(
            movement.x * (Time.deltaTime * playerSpeed),
            0,
            movement.y * (Time.deltaTime * playerSpeed));
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        
        _rigidbody.MovePosition(transform.position + direction);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            rotation,
            rotationSpeed * Time.deltaTime);
    }

    private void StepClimb()
    { 
        // directions that we need to cast rays to ensure we can climb stair from any angle
        
        Vector3[] directions = new Vector3[]
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 1f),
            new Vector3(-1f, 0f, 1f)
        };
        
        // if the bottom raycast collides but the top doesn't then bounce us up over the step
        foreach (Vector3 direction in directions)
        {
            Debug.DrawRay(stepRaycastLower.position, transform.TransformDirection(direction), Color.green);
            Debug.DrawRay(stepRaycastUpper.position, transform.TransformDirection(direction), Color.red);
            
            if (Physics.Raycast(stepRaycastLower.position, transform.TransformDirection(direction), stairMaxDistance)) 
            { 
                if (!Physics.Raycast(stepRaycastUpper.position, transform.TransformDirection(direction), stairDeepness))
                { 
                    _rigidbody.position -= new Vector3(0f, -stepSmooth * Time.deltaTime, 0f);
                }
            }
        }
    }
}
