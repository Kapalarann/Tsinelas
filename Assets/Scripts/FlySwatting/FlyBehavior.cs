using System;
using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace Tsinelas.FlySwatting
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class FlyBehavior : MonoBehaviour
    {
        [Header("Physics & Joint")]
        [Tooltip("The strength of the spring joint pulling the fly towards the anchor.")]
        public float springStrength = 150f;
        [Tooltip("The damper value of the spring joint to reduce oscillation.")]
        public float springDamper = 10f;

        [Header("Movement Settings")]
        [Tooltip("Radius around the current area center where the anchor wanders.")]
        public float wanderRadius = 1.2f;
        [Tooltip("How fast the anchor moves towards its wander targets.")]
        public float anchorSpeed = 2.5f;
        [Tooltip("How long to spend in one area before switching to the next.")]
        public float areaSwitchInterval = 7f;

        [Header("Downed Behavior")]
        [Tooltip("The gravity scale or drag adjustment when downed.")]
        public float downedLinearDrag = 0.5f;
        [Tooltip("Force applied on impact when hit.")]
        public float impactSpinForce = 15f;

        [Header("VFX")]
        [Tooltip("VFX Graph asset (.vfx) to play at the fly's position when hit.")]
        public VisualEffectAsset bloodSplatterAsset;

        // Fired when this fly is hit and downed
        public event Action<FlyBehavior> OnFlyDowned;

        public bool IsDowned { get; private set; } = false;
        private bool _vfxPlayed = false;

        private Rigidbody _rb;
        private SpringJoint _springJoint;
        private GameObject _anchorObj;
        private Rigidbody _anchorRb;

        private Vector3[] _areaCenters;
        private int _currentAreaIndex = 0;
        private Vector3 _currentTargetPos;
        private Vector3 _currentAreaCenter;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false; // Start floating/flying
            _rb.linearDamping = 1f;
            _rb.angularDamping = 1f;
        }

        /// <summary>
        /// Initializes the fly with area centers it can fly between.
        /// </summary>
        public void Initialize(Vector3[] areas, int startAreaIndex)
        {
            _areaCenters = areas;
            if (_areaCenters != null && _areaCenters.Length > 0)
            {
                _currentAreaIndex = startAreaIndex % _areaCenters.Length;
                _currentAreaCenter = _areaCenters[_currentAreaIndex];
            }
            else
            {
                _currentAreaCenter = transform.position;
            }

            // Create hidden anchor
            _anchorObj = new GameObject($"FlyAnchor_{gameObject.name}");
            _anchorObj.transform.position = transform.position;
            
            _anchorRb = _anchorObj.AddComponent<Rigidbody>();
            _anchorRb.isKinematic = true;
            _anchorRb.useGravity = false;

            // Connect via SpringJoint
            _springJoint = gameObject.AddComponent<SpringJoint>();
            _springJoint.connectedBody = _anchorRb;
            _springJoint.autoConfigureConnectedAnchor = false;
            _springJoint.anchor = Vector3.zero;
            _springJoint.connectedAnchor = Vector3.zero;
            _springJoint.spring = springStrength;
            _springJoint.damper = springDamper;
            _springJoint.minDistance = 0f;
            _springJoint.maxDistance = 0.05f;

            // Start movement coroutines
            StartCoroutine(WanderRoutine());
            StartCoroutine(AreaSwitchRoutine());
        }

        private IEnumerator WanderRoutine()
        {
            while (!IsDowned)
            {
                // Choose a random position inside a sphere around current area center
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * wanderRadius;
                // Keep heights relatively reasonable/level
                randomOffset.y = Mathf.Clamp(randomOffset.y, -wanderRadius * 0.5f, wanderRadius * 0.5f);
                _currentTargetPos = _currentAreaCenter + randomOffset;

                // Move anchor towards target position
                while (_anchorObj != null && Vector3.Distance(_anchorObj.transform.position, _currentTargetPos) > 0.1f && !IsDowned)
                {
                    Vector3 nextPos = Vector3.MoveTowards(
                        _anchorObj.transform.position,
                        _currentTargetPos,
                        anchorSpeed * Time.deltaTime
                    );
                    if (_anchorRb != null)
                    {
                        _anchorRb.MovePosition(nextPos);
                    }
                    yield return null;
                }

                // Brief pause at the wander target
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 1f));
            }
        }

        private IEnumerator AreaSwitchRoutine()
        {
            while (!IsDowned)
            {
                yield return new WaitForSeconds(areaSwitchInterval);

                if (IsDowned) yield break;

                // Switch to next area center
                if (_areaCenters != null && _areaCenters.Length > 1)
                {
                    _currentAreaIndex = (_currentAreaIndex + 1) % _areaCenters.Length;
                    _currentAreaCenter = _areaCenters[_currentAreaIndex];
                    Debug.Log($"{gameObject.name}: Switching to area {_currentAreaIndex} at {_currentAreaCenter}");
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsDowned) return;

            // Check if hit by slipper
            var slipper = collision.gameObject.GetComponentInParent<Tsinelas.TumbangPreso.TumbangPresoSlipper>();
            if (slipper != null)
            {
                DownFly();
            }
        }

        private void DownFly()
        {
            IsDowned = true;
            Debug.Log($"{gameObject.name} was hit and downed!");

            // Break spring joint
            if (_springJoint != null)
            {
                Destroy(_springJoint);
            }

            // Cleanup anchor
            if (_anchorObj != null)
            {
                Destroy(_anchorObj);
            }

            // Spawn blood splatter VFX — exactly once, on the slipper hit that downed the fly
            if (!_vfxPlayed && bloodSplatterAsset != null)
            {
                _vfxPlayed = true;
                GameObject vfxGo = new GameObject("BloodSplatter_VFX");
                vfxGo.transform.position = transform.position;
                VisualEffect vfx = vfxGo.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = bloodSplatterAsset;
                vfx.Play();
                Destroy(vfxGo, 3f);
            }

            // Fall with physics
            _rb.useGravity = true;
            _rb.linearDamping = downedLinearDrag;
            
            // Add a fun spin / physics impact
            _rb.AddTorque(UnityEngine.Random.onUnitSphere * impactSpinForce, ForceMode.Impulse);
            _rb.AddForce(Vector3.down * 2f, ForceMode.Impulse);

            OnFlyDowned?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (_anchorObj != null)
            {
                Destroy(_anchorObj);
            }
        }
    }
}
