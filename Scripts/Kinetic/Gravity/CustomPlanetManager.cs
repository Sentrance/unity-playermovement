using System.Collections.Generic;
using KinematicCharacterController;
using Kinetic.Teleporter;
using UnityEngine;

namespace Kinetic.Gravity
{
    public class CustomPlanetManager : MonoBehaviour, IMoverController
    {
        public PhysicsMover planetMover;
        public float gravityStrength = 10;
        public Vector3 orbitAxis = Vector3.forward;
        public float orbitSpeed = 10;
        public bool snapCharacterToPlanet;
        
        private List<PlayerCharacterController> _characterControllersOnPlanet = new List<PlayerCharacterController>();
        private Vector3 _savedGravity;
        private Vector3 _lastPlanetGravity;
        private Quaternion _lastRotation;

        private void Start()
        {
            _lastRotation = planetMover.transform.rotation;

            planetMover.MoverController = this;
        }

        public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
        {
            goalPosition = planetMover.Rigidbody.position;

            // Rotate
            Quaternion targetRotation = Quaternion.Euler(orbitAxis * (orbitSpeed * deltaTime)) * _lastRotation;
            goalRotation = targetRotation;
            _lastRotation = targetRotation;

            // Apply gravity to characters
            foreach (PlayerCharacterController cc in _characterControllersOnPlanet)
            {
                cc.gravity = (planetMover.transform.position - cc.transform.position).normalized * gravityStrength;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            PlayerCharacterController cc = other.GetComponent<PlayerCharacterController>();
            if (cc)
            {
                ControlGravity(cc);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            PlayerCharacterController cc = other.GetComponent<PlayerCharacterController>();
            if (cc)
            {
                UnControlGravity(cc);
            }
        }


        void ControlGravity(PlayerCharacterController cc)
        {
            _savedGravity = cc.gravity;
            _characterControllersOnPlanet.Add(cc);
            if (snapCharacterToPlanet)
                cc.gravityOrientationMethod = GravityOrientationMethod.TowardsGroundSlopeAndGravity;
        }

        void UnControlGravity(PlayerCharacterController cc)
        {
            cc.gravity = _savedGravity;
            _characterControllersOnPlanet.Remove(cc);
            if (snapCharacterToPlanet)
                cc.gravityOrientationMethod = GravityOrientationMethod.TowardsGravity;
        }
    }
}