using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Tsinelas.TumbangPreso
{
    /// <summary>
    /// Attached to each slipper prefab. Tracks whether the slipper is currently
    /// held by the player and reports this state to TumbangPresoGameManager.
    /// Works with XR Interaction Toolkit's XRGrabInteractable.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class TumbangPresoSlipper : MonoBehaviour
    {
        /// <summary>
        /// True when the player is actively holding this slipper.
        /// </summary>
        public bool IsHeld { get; private set; } = false;

        /// <summary>
        /// True when the player has released/thrown this slipper.
        /// </summary>
        public bool HasBeenThrown { get; private set; } = false;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            // Subscribe to XRI grab and release events
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
            Debug.Log($"TumbangPresoSlipper: {gameObject.name} was grabbed.");
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            IsHeld = false;
            HasBeenThrown = true;
            if (_grabInteractable != null)
            {
                _grabInteractable.enabled = false;
            }
            Debug.Log($"TumbangPresoSlipper: {gameObject.name} was released/thrown and can no longer be grabbed.");
        }
    }
}
