using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tsinelas.TumbangPreso
{
    /// <summary>
    /// Coordinates the Tumbang Preso game state: spawning slippers, tracking their status,
    /// responding to can knockdown events, and managing the heart-based lose condition.
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


        [Tooltip("How long to wait after all slippers are spent before spawning the next batch.")]
        public float respawnDelay = 1.0f;

        [Header("References")]
        [Tooltip("The tin can object in the scene.")]
        public TumbangPresoCan can;

        [Header("Win / Lose")]
        [Tooltip("Starting number of hearts. Each slipper respawn costs 1 heart.")]
        public int maxHearts = 3;

        [Tooltip("Prefab for the win panel. Must have TumbangPresoWinPanel attached.")]
        public GameObject winPanelPrefab;

        [Tooltip("Prefab for the lose panel. Must have TumbangPresoLosePanel attached.")]
        public GameObject losePanelPrefab;

        /// <summary>
        /// Fired whenever the player's heart count changes.
        /// Parameters: (int currentHearts, int maxHearts)
        /// Hook this up to a HUD to display remaining lives.
        /// </summary>
        public event Action<int, int> OnHeartsChanged;
        public event Action OnGameWon;
        public event Action OnGameLost;

        // Internal state
        private List<TumbangPresoSlipper> _activeSlippers = new List<TumbangPresoSlipper>();
        private Transform _playerTransform;
        private bool _isCheckingForEmpty = false;
        private bool _gameOver = false;   // true when either win or lose has triggered
        private int _currentHearts;



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

            // Initialise hearts
            _currentHearts = maxHearts;
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);

            // Spawn starting slippers at player's feet
            SpawnSlipperBatch(useInitialSpawn: true);
        }

        private void Update()
        {
            // Continuously check if all slippers are spent (thrown far from player and not held)
            if (!_isCheckingForEmpty && !_gameOver && _activeSlippers.Count > 0)
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

                // A slipper is NOT spent if: it's being held by the player OR it hasn't been thrown yet
                if (slipper.IsHeld || !slipper.HasBeenThrown) return false;
            }

            return true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            _isCheckingForEmpty = true;

            yield return new WaitForSeconds(respawnDelay);

            if (_gameOver)
            {
                _isCheckingForEmpty = false;
                yield break;
            }

            // Deduct a heart for this respawn
            _currentHearts--;
            Debug.Log($"TumbangPresoGameManager: Heart lost. Hearts remaining: {_currentHearts}/{maxHearts}");
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);

            if (_currentHearts <= 0)
            {
                // No hearts left — show the lose panel
                ShowLose();
                _isCheckingForEmpty = false;
                yield break;
            }

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
                    UnityEngine.Random.Range(-15f, 15f),
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(-15f, 15f)
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

        // ─── Win / Lose ────────────────────────────────────────────────────────────

        private void HandleCanKnockedDown()
        {
            if (_gameOver) return;
            _gameOver = true;

            Debug.Log("TumbangPresoGameManager: CAN KNOCKED DOWN! Player wins!");
            ShowWin();
        }

        private void ShowWin()
        {
            OnGameWon?.Invoke();

            if (winPanelPrefab == null)
            {
                Debug.LogWarning("TumbangPresoGameManager: Win Panel Prefab is not assigned.");
                return;
            }

            Instantiate(winPanelPrefab);
        }

        private void ShowLose()
        {
            _gameOver = true;
            Debug.Log("TumbangPresoGameManager: Out of hearts! Player loses.");

            OnGameLost?.Invoke();

            if (losePanelPrefab == null)
            {
                Debug.LogWarning("TumbangPresoGameManager: Lose Panel Prefab is not assigned.");
                return;
            }

            Instantiate(losePanelPrefab);
        }

        // ─── Retry ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resets the game to its initial state without reloading the scene.
        /// Called by TumbangPresoResultPanel when the player presses Retry.
        /// </summary>
        public void ResetGame()
        {
            // Reset state flags
            _gameOver = false;
            _isCheckingForEmpty = false;

            // Restore hearts
            _currentHearts = maxHearts;
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);
            Debug.Log($"TumbangPresoGameManager: Hearts restored to {maxHearts}.");

            // Reset the can
            if (can != null)
                can.ResetCan();

            // Destroy all currently tracked slippers and spawn a fresh batch
            foreach (var slipper in _activeSlippers)
            {
                if (slipper != null)
                    Destroy(slipper.gameObject);
            }
            _activeSlippers.Clear();

            SpawnSlipperBatch(useInitialSpawn: true);

            Debug.Log("TumbangPresoGameManager: Game reset.");
        }

        // ─── Cleanup ───────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (can != null)
                can.OnCanKnockedDown -= HandleCanKnockedDown;
        }
    }
}
