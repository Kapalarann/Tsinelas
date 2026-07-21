using System;
using UnityEngine;

namespace Tsinelas.TumbangPreso
{
    /// <summary>
    /// Attached to the tin can physics object. Detects when it has been knocked over
    /// by a slipper collision and fires an event the GameManager can listen to.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TumbangPresoCan : MonoBehaviour
    {
        [Header("Knockdown Settings")]
        [Tooltip("The tilt angle (in degrees from world Up) at which the can is considered knocked over.")]
        public float knockdownAngle = 45.0f;

        /// <summary>
        /// Fired once when the can is knocked over.
        /// </summary>
        public event Action OnCanKnockedDown;
        public event Action OnCanHit;

        private bool _isKnockedDown = false;
        private Rigidbody _rb;

        // Cached spawn transform for resetting on Retry
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        private void Update()
        {
            if (_isKnockedDown) return;

            // Check tilt angle: measure how far the can's up vector has deviated from world up
            float tiltAngle = Vector3.Angle(transform.up, Vector3.up);

            if (tiltAngle >= knockdownAngle)
            {
                RegisterKnockdown();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isKnockedDown) return;

            // Only react to collisions from objects that have a TumbangPresoSlipper component.
            // This avoids requiring a manually registered Unity tag.
            if (collision.gameObject.GetComponent<TumbangPresoSlipper>() != null)
            {
                float impactForce = collision.impulse.magnitude;
                Debug.Log($"TumbangPresoCan: Hit by slipper '{collision.gameObject.name}' with impulse {impactForce:F2}N.");
                
                OnCanHit?.Invoke();
                
                // Knockdown is determined via tilt angle check in Update, not directly here,
                // so the physics simulation can fully resolve first.
            }
        }

        private void RegisterKnockdown()
        {
            _isKnockedDown = true;
            Debug.Log("TumbangPresoCan: Can has been knocked down!");
            OnCanKnockedDown?.Invoke();
        }

        /// <summary>
        /// Restores the can to its original position and upright rotation.
        /// Call this from TumbangPresoGameManager.ResetGame() on Retry.
        /// </summary>
        public void ResetCan()
        {
            // Temporarily make kinematic so we can teleport without physics fighting us
            _rb.isKinematic = true;
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;
            _rb.isKinematic = false;

            // Clear any lingering velocity
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _isKnockedDown = false;
            Debug.Log("TumbangPresoCan: Can has been reset.");
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize the knockdown tilt in the editor
            Gizmos.color = Color.red;
            Vector3 tiltDir = Quaternion.AngleAxis(knockdownAngle, transform.right) * Vector3.up;
            Gizmos.DrawRay(transform.position, tiltDir * 0.5f);
            tiltDir = Quaternion.AngleAxis(-knockdownAngle, transform.right) * Vector3.up;
            Gizmos.DrawRay(transform.position, tiltDir * 0.5f);
        }
    }
}

