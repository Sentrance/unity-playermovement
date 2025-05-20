using UnityEngine;
using UnityEngine.Events;

namespace Kinetic.Teleporter
{
    public class CustomTeleporter : MonoBehaviour
    {
        public CustomTeleporter TeleportTo;

        public UnityAction<PlayerCharacterController> OnCharacterTeleport;

        public bool isBeingTeleportedTo { get; set; }

        private void OnTriggerEnter(Collider other)
        {
            if (!isBeingTeleportedTo)
            {
                PlayerCharacterController cc = other.GetComponent<PlayerCharacterController>();
                if (cc)
                {
                    var destination = TeleportTo.transform;
                    cc.motor.SetPositionAndRotation(destination.position, destination.rotation);

                    OnCharacterTeleport?.Invoke(cc);

                    TeleportTo.isBeingTeleportedTo = true;
                }
            }

            isBeingTeleportedTo = false;
        }
    }
}