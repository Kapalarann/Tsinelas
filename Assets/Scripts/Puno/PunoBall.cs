using System;
using UnityEngine;

namespace Tsinelas.Puno
{
    /// <summary>
    /// Attached to the ball in the Puno Minigame.
    /// Detects when the spring joint breaks and fires an event.
    /// Also supports resetting back to its starting position / joint state.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PunoBall : MonoBehaviour
    {
        public event Action OnBallFreed;

        private Rigidbody _rb;
        private SpringJoint _joint;

        // Saved initial state
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        // Saved joint settings (captured from whatever joint exists at Start)
        private Rigidbody _jointConnectedBody;
        private Vector3 _jointAnchor;
        private Vector3 _jointConnectedAnchor;
        private float _jointSpring;
        private float _jointDamper;
        private float _jointMinDistance;
        private float _jointMaxDistance;
        private float _jointBreakForce;

        private bool _hasBeenFreed = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;

            // Capture the SpringJoint settings so we can recreate it on reset
            _joint = GetComponent<SpringJoint>();
            if (_joint != null)
            {
                _jointConnectedBody   = _joint.connectedBody;
                _jointAnchor          = _joint.anchor;
                _jointConnectedAnchor = _joint.connectedAnchor;
                _jointSpring          = _joint.spring;
                _jointDamper          = _joint.damper;
                _jointMinDistance     = _joint.minDistance;
                _jointMaxDistance     = _joint.maxDistance;
                _jointBreakForce      = _joint.breakForce;
            }
            else
            {
                Debug.LogWarning("PunoBall: No SpringJoint found on Start — reset will reposition but won't recreate the joint.");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            var slipper = collision.gameObject.GetComponentInParent<Tsinelas.TumbangPreso.TumbangPresoSlipper>();
            if (slipper != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBallHit(transform.position);
            }
        }

        private void OnJointBreak(float breakForce)
        {
            if (_hasBeenFreed) return;
            _hasBeenFreed = true;
            Debug.Log($"PunoBall: Joint broke with force {breakForce}");
            OnBallFreed?.Invoke();
        }

        /// <summary>
        /// Resets the ball to its original position and recreates the SpringJoint.
        /// Call this from PunoGameManager.ResetGame().
        /// </summary>
        public void ResetBall()
        {
            _hasBeenFreed = false;

            // Stop all motion
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity       = false;

            // Teleport back to start
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;

            // Destroy any leftover joint (broken joints leave a destroyed component)
            var existingJoint = GetComponent<SpringJoint>();
            if (existingJoint != null)
                Destroy(existingJoint);

            // Recreate the joint with the original settings
            if (_jointConnectedBody != null)
            {
                _joint = gameObject.AddComponent<SpringJoint>();
                _joint.connectedBody          = _jointConnectedBody;
                _joint.autoConfigureConnectedAnchor = false;
                _joint.anchor                 = _jointAnchor;
                _joint.connectedAnchor        = _jointConnectedAnchor;
                _joint.spring                 = _jointSpring;
                _joint.damper                 = _jointDamper;
                _joint.minDistance            = _jointMinDistance;
                _joint.maxDistance            = _jointMaxDistance;
                _joint.breakForce             = _jointBreakForce;

                // Re-enable gravity so the ball hangs naturally from the joint
                _rb.useGravity = true;
            }
            else
            {
                // No joint was ever configured — just leave it at the reset position
                _rb.useGravity = true;
                Debug.LogWarning("PunoBall: Could not recreate SpringJoint on reset — connected body reference is missing.");
            }

            Debug.Log("PunoBall: Ball reset to initial position.");
        }
    }
}
