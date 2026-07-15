using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Tsinelas.FlySwatting
{
    /// <summary>
    /// Attached to the slipper prefab used in the Fly Swatting mini-game.
    /// Unlike TumbangPresoSlipper, this slipper is meant to be held and swung
    /// there is no throw/spent logic. The player keeps holding it to swat flies.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class FlySwattingSlipper : MonoBehaviour
    {
        /// <summary>
        /// True when the player is actively holding this slipper.
        /// </summary>
        public bool IsHeld { get; private set; } = false;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;

        private void Awake()
        {
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                _grabInteractable.selectExited.RemoveListener(OnReleased);
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            IsHeld = true;
            Debug.Log($"FlySwattingSlipper: {gameObject.name} grabbed.");
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            IsHeld = false;
            Debug.Log($"FlySwattingSlipper: {gameObject.name} released.");
        }
    }
}
