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

        private bool _isKnockedDown = false;

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
