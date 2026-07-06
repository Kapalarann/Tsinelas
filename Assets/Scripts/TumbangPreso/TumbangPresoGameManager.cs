using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tsinelas.TumbangPreso
{
    /// <summary>
    /// Coordinates the Tumbang Preso game state: spawning slippers, tracking their status,
    /// and responding to can knockdown events.
    /// </summary>
    public class TumbangPresoGameManager : MonoBehaviour
    {
        [Header("Slipper Spawning")]
        [Tooltip("Prefab for the throwable slipper. Must have TumbangPresoSlipper component attached.")]
        public GameObject slipperPrefab;

        [Tooltip("How many slippers to give the player at a time.")]
        public int slippersPerBatch = 2;

        [Tooltip("Offset from the player's feet where the initial slippers will spawn.")]
        public Vector3 initialSpawnOffset = new Vector3(0f, 0.05f, 0f);

        [Tooltip("How far in front and above the player the respawn slippers appear before falling.")]
        public Vector3 respawnOffset = new Vector3(0f, 1.2f, 0.6f);

        [Tooltip("How far apart (horizontal) the two respawn slippers are spaced.")]
        public float respawnSpread = 0.3f;

        [Header("Slipper Detection")]
        [Tooltip("Distance from the player within which a slipper is still considered 'available' (not yet spent).")]
        public float slipperRecallRange = 5.0f;

        [Tooltip("How long to wait after all slippers are spent before spawning the next batch.")]
        public float respawnDelay = 1.0f;

        [Header("References")]
        [Tooltip("The tin can object in the scene.")]
        public TumbangPresoCan can;

        // Tracks all currently active slipper instances
        private List<TumbangPresoSlipper> _activeSlippers = new List<TumbangPresoSlipper>();
        private Transform _playerTransform;
        private bool _isCheckingForEmpty = false;
        private bool _gameWon = false;

        private void Start()
        {
            // Use the Main Camera as a proxy for the player's position (headset = body center in VR)
            if (Camera.main != null)
                _playerTransform = Camera.main.transform;

            if (slipperPrefab == null)
            {
                Debug.LogError("TumbangPresoGameManager: Slipper Prefab is not assigned!");
                return;
            }

            if (can == null)
            {
                Debug.LogError("TumbangPresoGameManager: TumbangPresoCan reference is not assigned!");
                return;
            }

            // Register for can knocked-down event
            can.OnCanKnockedDown += HandleCanKnockedDown;

            // Spawn starting slippers at player's feet
            SpawnSlipperBatch(useInitialSpawn: true);
        }

        private void Update()
        {
            // Continuously check if all slippers are spent (thrown far from player and not held)
            if (!_isCheckingForEmpty && !_gameWon && _activeSlippers.Count > 0)
            {
                if (AllSlippersSpent())
                {
                    StartCoroutine(RespawnAfterDelay());
                }
            }
        }

        private bool AllSlippersSpent()
        {
            if (_playerTransform == null) return false;

            foreach (var slipper in _activeSlippers)
            {
                if (slipper == null) continue;

                // A slipper is NOT spent if: it's being held by the player OR it's within recall range
                if (slipper.IsHeld) return false;

                float dist = Vector3.Distance(slipper.transform.position, _playerTransform.position);
                if (dist < slipperRecallRange) return false;
            }

            return true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            _isCheckingForEmpty = true;

            yield return new WaitForSeconds(respawnDelay);

            // Clean up old slipper references (they remain in the scene but are no longer tracked)
            _activeSlippers.Clear();

            // Spawn fresh batch in front of the player
            SpawnSlipperBatch(useInitialSpawn: false);

            _isCheckingForEmpty = false;
        }

        private void SpawnSlipperBatch(bool useInitialSpawn)
        {
            if (_playerTransform == null) return;

            for (int i = 0; i < slippersPerBatch; i++)
            {
                Vector3 spawnPos;

                if (useInitialSpawn)
                {
                    // Spawn at player's feet, slightly spread apart
                    float xOffset = (i == 0) ? -0.15f : 0.15f;
                    spawnPos = _playerTransform.position
                        + initialSpawnOffset
                        + new Vector3(xOffset, 0, 0);
                }
                else
                {
                    // Spawn in front of and above the player, spread apart, then let them fall
                    float xOffset = (i == 0) ? -respawnSpread : respawnSpread;
                    Vector3 forward = new Vector3(
                        _playerTransform.forward.x,
                        0,
                        _playerTransform.forward.z
                    ).normalized;

                    spawnPos = _playerTransform.position
                        + forward * respawnOffset.z
                        + Vector3.up * respawnOffset.y
                        + _playerTransform.right * xOffset;
                }

                // Spawn with a slight random rotation for visual variety
                Quaternion randomRot = Quaternion.Euler(
                    Random.Range(-15f, 15f),
                    Random.Range(0f, 360f),
                    Random.Range(-15f, 15f)
                );

                GameObject slipperGo = Instantiate(slipperPrefab, spawnPos, randomRot);

                TumbangPresoSlipper slipper = slipperGo.GetComponent<TumbangPresoSlipper>();
                if (slipper != null)
                {
                    _activeSlippers.Add(slipper);
                }
                else
                {
                    Debug.LogWarning("TumbangPresoGameManager: Spawned slipper prefab is missing TumbangPresoSlipper component.");
                }
            }

            Debug.Log($"TumbangPresoGameManager: Spawned {slippersPerBatch} slipper(s). UseInitialSpawn={useInitialSpawn}");
        }

        private void HandleCanKnockedDown()
        {
            if (_gameWon) return;
            _gameWon = true;

            Debug.Log("TumbangPresoGameManager: CAN KNOCKED DOWN! Player wins!");
            // TODO: Trigger win UI, score screen, or return to hub
        }

        private void OnDestroy()
        {
            if (can != null)
                can.OnCanKnockedDown -= HandleCanKnockedDown;
        }
    }
}
