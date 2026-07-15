using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tsinelas.FlySwatting
{
    public class FlySwattingGameManager : MonoBehaviour
    {
        [Header("Slipper Spawning")]
        [Tooltip("Prefab for the swatter slipper. Must have FlySwattingSlipper component attached.")]
        public GameObject slipperPrefab;
        [Tooltip("How many slippers to give the player at a time.")]
        public int slippersPerBatch = 2;
        [Tooltip("Offset from the player's feet where the initial slippers will spawn.")]
        public Vector3 initialSpawnOffset = new Vector3(0f, 0.05f, 0f);
        [Tooltip("How far in front and above the player the respawn slippers appear before falling.")]
        public Vector3 respawnOffset = new Vector3(0f, 1.2f, 0.6f);
        [Tooltip("How far apart (horizontal) the two respawn slippers are spaced.")]
        public float respawnSpread = 0.3f;
        [Tooltip("How long to wait after all slippers are spent before spawning the next batch.")]
        public float respawnDelay = 1.0f;

        [Header("Fly Settings")]
        [Tooltip("Prefab for the fly. Must have FlyBehavior component attached.")]
        public GameObject flyPrefab;
        [Tooltip("All possible fly area transforms (set up to 15 in the Inspector). A random subset is chosen each round.")]
        public Transform[] areaCenters;
        [Tooltip("Exactly how many flies to spawn per round. Each fly gets its own randomly chosen area.")]
        public int fliesToSpawn = 5;


        [Header("Win / Lose")]
        [Tooltip("Starting number of hearts. Each slipper respawn costs 1 heart.")]
        public int maxHearts = 3;
        public GameObject winPanelPrefab;
        public GameObject losePanelPrefab;

        [Header("Time Limit Settings")]
        [Tooltip("Should the game be time-based instead of heart-based?")]
        public bool useTimeLimit = false;
        [Tooltip("Time limit in seconds if useTimeLimit is enabled.")]
        public float timeLimit = 60f;

        public event Action<int, int> OnHeartsChanged;
        public event Action<int, int> OnFliesDownedChanged; // (int downedCount, int totalCount)
        public event Action<float> OnTimeChanged; // (float timeRemaining)

        // Computed at spawn time — total flies this round
        private int _totalFliesToDown = 0;
        private float _timeRemaining;

        private List<FlySwattingSlipper> _activeSlippers = new List<FlySwattingSlipper>();
        private List<FlyBehavior> _spawnedFlies = new List<FlyBehavior>();
        private Transform _playerTransform;

        private bool _gameOver = false;
        private int _currentHearts;
        private int _downedFliesCount = 0;

        private void Start()
        {
            if (Camera.main != null)
                _playerTransform = Camera.main.transform;

            if (slipperPrefab == null)
            {
                Debug.LogError("FlySwattingGameManager: Slipper Prefab is not assigned!");
                return;
            }

            if (flyPrefab == null)
            {
                Debug.LogError("FlySwattingGameManager: Fly Prefab is not assigned!");
                return;
            }

            if (areaCenters == null || areaCenters.Length == 0)
            {
                Debug.LogError("FlySwattingGameManager: Area Centers are not assigned!");
                return;
            }

            // Setup game
            StartGame();
        }

        private void Update()
        {
            if (!_gameOver && useTimeLimit)
            {
                _timeRemaining -= Time.deltaTime;
                OnTimeChanged?.Invoke(_timeRemaining);

                if (_timeRemaining <= 0f)
                {
                    _timeRemaining = 0f;
                    Debug.Log("FlySwattingGameManager: Time ran out!");
                    ShowLose();
                }
            }
        }



        private void SpawnSlipperBatch(bool useInitialSpawn)
        {
            if (_playerTransform == null) return;

            for (int i = 0; i < slippersPerBatch; i++)
            {
                Vector3 spawnPos;

                if (useInitialSpawn)
                {
                    float xOffset = (i == 0) ? -0.15f : 0.15f;
                    spawnPos = _playerTransform.position + initialSpawnOffset + new Vector3(xOffset, 0, 0);
                }
                else
                {
                    float xOffset = (i == 0) ? -respawnSpread : respawnSpread;
                    Vector3 forward = new Vector3(_playerTransform.forward.x, 0, _playerTransform.forward.z).normalized;
                    spawnPos = _playerTransform.position + forward * respawnOffset.z + Vector3.up * respawnOffset.y + _playerTransform.right * xOffset;
                }

                Quaternion randomRot = Quaternion.Euler(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-15f, 15f));
                GameObject slipperGo = Instantiate(slipperPrefab, spawnPos, randomRot);

                FlySwattingSlipper slipper = slipperGo.GetComponent<FlySwattingSlipper>();
                if (slipper != null)
                {
                    _activeSlippers.Add(slipper);
                }
                else
                {
                    Debug.LogWarning("FlySwattingGameManager: Spawned slipper prefab is missing FlySwattingSlipper component.");
                }
            }

            Debug.Log($"FlySwattingGameManager: Spawned {slippersPerBatch} slipper(s). UseInitialSpawn={useInitialSpawn}");
        }

        /// <summary>
        /// Spawns flies. If <paramref name="destroyExisting"/> is true, all current
        /// fly GameObjects are destroyed first (fresh start). If false, only areas
        /// that have no living fly are used for new spawns — downed flies are left
        /// untouched on the ground (retry behaviour).
        /// </summary>
        private void SpawnFlies(bool destroyExisting)
        {
            if (destroyExisting)
            {
                // Full reset: destroy every fly
                foreach (var fly in _spawnedFlies)
                {
                    if (fly != null)
                        Destroy(fly.gameObject);
                }
                _spawnedFlies.Clear();
            }
            else
            {
                // Retry: unsubscribe downed flies and remove them from the tracked list,
                // but do NOT destroy their GameObjects so they stay on the ground.
                _spawnedFlies.RemoveAll(fly =>
                {
                    if (fly == null || fly.IsDowned)
                    {
                        if (fly != null)
                            fly.OnFlyDowned -= HandleFlyDowned;
                        return true; // remove from list
                    }
                    return false; // keep living flies
                });
            }

            _downedFliesCount = 0;
            _totalFliesToDown = 0;

            if (areaCenters == null || areaCenters.Length == 0)
            {
                Debug.LogError("FlySwattingGameManager: No area centers assigned!");
                return;
            }

            // Build a pool of areas that don't already contain a living fly
            List<Transform> pool = new List<Transform>();
            foreach (Transform area in areaCenters)
            {
                bool occupied = false;
                foreach (var livingFly in _spawnedFlies)
                {
                    if (livingFly != null && !livingFly.IsDowned &&
                        Vector3.Distance(livingFly.transform.position, area.position) < 0.5f)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (!occupied)
                    pool.Add(area);
            }

            // How many new flies do we need?
            int needed = fliesToSpawn - _spawnedFlies.Count;
            int pickCount = Mathf.Min(needed, pool.Count);

            if (pickCount <= 0)
            {
                // All required flies are already alive; just recount
                _totalFliesToDown = _spawnedFlies.Count;
                OnFliesDownedChanged?.Invoke(_downedFliesCount, _totalFliesToDown);
                return;
            }

            // Fisher-Yates shuffle the available pool
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Transform tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            List<Transform> selected = pool.GetRange(0, pickCount);

            // Build patrol area array from ALL area centers (living + new)
            Vector3[] patrolAreas = new Vector3[areaCenters.Length];
            for (int i = 0; i < areaCenters.Length; i++)
                patrolAreas[i] = areaCenters[i].position;

            // Spawn new flies
            for (int i = 0; i < selected.Count; i++)
            {
                Vector3 spawnPos = selected[i].position;

                GameObject flyGo = Instantiate(flyPrefab, spawnPos, Quaternion.identity);
                FlyBehavior fly = flyGo.GetComponent<FlyBehavior>();
                if (fly != null)
                {
                    fly.Initialize(patrolAreas, i);
                    fly.OnFlyDowned += HandleFlyDowned;
                    _spawnedFlies.Add(fly);
                }
                else
                {
                    Debug.LogError("FlySwattingGameManager: Fly prefab is missing FlyBehavior component!");
                    Destroy(flyGo);
                }
            }

            _totalFliesToDown = _spawnedFlies.Count;
            OnFliesDownedChanged?.Invoke(_downedFliesCount, _totalFliesToDown);
            Debug.Log($"FlySwattingGameManager: Spawned {pickCount} new fly/flies. Total living: {_totalFliesToDown}.");
        }



        private void HandleFlyDowned(FlyBehavior fly)
        {
            if (_gameOver) return;

            fly.OnFlyDowned -= HandleFlyDowned;
            _downedFliesCount++;
            OnFliesDownedChanged?.Invoke(_downedFliesCount, _totalFliesToDown);

            Debug.Log($"FlySwattingGameManager: Fly downed. Progress: {_downedFliesCount}/{_totalFliesToDown}");

            if (_downedFliesCount >= _totalFliesToDown)
            {
                _gameOver = true;
                ShowWin();
            }
        }

        private void ShowWin()
        {
            Debug.Log("FlySwattingGameManager: All flies downed! Player wins!");
            if (winPanelPrefab == null)
            {
                Debug.LogWarning("FlySwattingGameManager: Win Panel Prefab is not assigned.");
                return;
            }
            Instantiate(winPanelPrefab);
        }

        private void ShowLose()
        {
            _gameOver = true;
            Debug.Log("FlySwattingGameManager: Out of hearts! Player loses.");

            if (losePanelPrefab == null)
            {
                Debug.LogWarning("FlySwattingGameManager: Lose Panel Prefab is not assigned.");
                return;
            }
            Instantiate(losePanelPrefab);
        }

        /// <summary>
        /// Full fresh start — destroys all existing flies and spawns a brand new set.
        /// Called once on Start().
        /// </summary>
        private void StartGame()
        {
            _gameOver = false;
            _currentHearts = maxHearts;
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);

            if (useTimeLimit)
            {
                _timeRemaining = timeLimit;
                OnTimeChanged?.Invoke(_timeRemaining);
            }

            // Clean up old slippers
            foreach (var slipper in _activeSlippers)
            {
                if (slipper != null)
                    Destroy(slipper.gameObject);
            }
            _activeSlippers.Clear();

            // Destroy any existing flies and spawn a fresh set
            SpawnFlies(destroyExisting: true);

            // Spawn fresh slippers
            SpawnSlipperBatch(useInitialSpawn: true);

            Debug.Log("FlySwattingGameManager: Game started fresh.");
        }

        /// <summary>
        /// Retry reset — keeps downed (dead) flies on the ground exactly where they fell.
        /// Resets hearts and slippers, then spawns new flies only at areas that no
        /// longer have a living fly.
        /// Call this from the Lose Panel's Retry button.
        /// </summary>
        public void ResetGame()
        {
            _gameOver = false;
            _currentHearts = maxHearts;
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);

            if (useTimeLimit)
            {
                _timeRemaining = timeLimit;
                OnTimeChanged?.Invoke(_timeRemaining);
            }

            // Clean up old slippers
            foreach (var slipper in _activeSlippers)
            {
                if (slipper != null)
                    Destroy(slipper.gameObject);
            }
            _activeSlippers.Clear();

            // Spawn new flies only where living flies no longer exist; keep downed ones
            SpawnFlies(destroyExisting: false);

            // Spawn fresh slippers
            SpawnSlipperBatch(useInitialSpawn: true);

            Debug.Log("FlySwattingGameManager: Game retried — dead flies kept on ground.");
        }

        private void OnDestroy()
        {
            foreach (var fly in _spawnedFlies)
            {
                if (fly != null)
                {
                    fly.OnFlyDowned -= HandleFlyDowned;
                }
            }
        }
    }
}
