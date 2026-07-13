using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tsinelas.TumbangPreso;

namespace Tsinelas.FlySwatting
{
    public class FlySwattingGameManager : MonoBehaviour
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
        [Tooltip("How long to wait after all slippers are spent before spawning the next batch.")]
        public float respawnDelay = 1.0f;

        [Header("Fly Settings")]
        [Tooltip("Prefab for the fly. Must have FlyBehavior component attached.")]
        public GameObject flyPrefab;
        [Tooltip("All possible fly area transforms (set up to 15 in the Inspector). A random subset is chosen each round.")]
        public Transform[] areaCenters;
        [Tooltip("Exactly how many flies to spawn per round. Each fly gets its own randomly chosen area.")]
        public int fliesToSpawn = 5;
        [Tooltip("Horizontal spawn dispersion radius around each area center.")]
        public float spawnRadius = 2.0f;
        [Tooltip("Vertical spawn offset range.")]
        public float spawnMinHeight = 1.0f;
        public float spawnMaxHeight = 2.5f;

        [Header("Win / Lose")]
        [Tooltip("Starting number of hearts. Each slipper respawn costs 1 heart.")]
        public int maxHearts = 3;
        public GameObject winPanelPrefab;
        public GameObject losePanelPrefab;

        public event Action<int, int> OnHeartsChanged;
        public event Action<int, int> OnFliesDownedChanged; // (int downedCount, int totalCount)

        // Computed at spawn time — total flies this round
        private int _totalFliesToDown = 0;

        private List<TumbangPresoSlipper> _activeSlippers = new List<TumbangPresoSlipper>();
        private List<FlyBehavior> _spawnedFlies = new List<FlyBehavior>();
        private Transform _playerTransform;
        private bool _isCheckingForEmpty = false;
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
            ResetGame();
        }

        private void Update()
        {
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

            _currentHearts--;
            Debug.Log($"FlySwattingGameManager: Heart lost. Hearts remaining: {_currentHearts}/{maxHearts}");
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);

            if (_currentHearts <= 0)
            {
                ShowLose();
                _isCheckingForEmpty = false;
                yield break;
            }

            _activeSlippers.Clear();
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

                TumbangPresoSlipper slipper = slipperGo.GetComponent<TumbangPresoSlipper>();
                if (slipper != null)
                {
                    _activeSlippers.Add(slipper);
                }
                else
                {
                    Debug.LogWarning("FlySwattingGameManager: Spawned slipper prefab is missing TumbangPresoSlipper component.");
                }
            }

            Debug.Log($"FlySwattingGameManager: Spawned {slippersPerBatch} slipper(s). UseInitialSpawn={useInitialSpawn}");
        }

        private void SpawnFlies()
        {
            // Clear existing flies
            foreach (var fly in _spawnedFlies)
            {
                if (fly != null)
                    Destroy(fly.gameObject);
            }
            _spawnedFlies.Clear();
            _downedFliesCount = 0;
            _totalFliesToDown = 0;

            if (areaCenters == null || areaCenters.Length == 0)
            {
                Debug.LogError("FlySwattingGameManager: No area centers assigned!");
                return;
            }

            // --- Pick fliesToSpawn random areas, 1 fly each ---
            int pickCount = Mathf.Min(fliesToSpawn, areaCenters.Length);
            List<Transform> pool = new List<Transform>(areaCenters);

            // Fisher-Yates shuffle
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Transform tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            List<Transform> selected = pool.GetRange(0, pickCount);

            // All selected positions are available as patrol targets
            Vector3[] patrolAreas = new Vector3[selected.Count];
            for (int i = 0; i < selected.Count; i++)
                patrolAreas[i] = selected[i].position;

            // Spawn exactly 1 fly per selected area
            for (int i = 0; i < selected.Count; i++)
            {
                Vector3 spawnPos = TryGetSafeSpawnPosition(selected[i].position);

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
            Debug.Log($"FlySwattingGameManager: Spawned {_totalFliesToDown} flies at {pickCount} random areas.");
        }

        /// <summary>
        /// Finds a valid airborne spawn position near <paramref name="areaCenter"/> by:
        /// 1. Picking a random horizontal offset within spawnRadius.
        /// 2. Raycasting downward from above to locate the actual floor.
        /// 3. Adding a random height above that floor.
        /// 4. Checking the result with CheckSphere to avoid spawning inside geometry.
        /// Falls back to directly above the area center if no clear position is found.
        /// </summary>
        private Vector3 TryGetSafeSpawnPosition(Vector3 areaCenter)
        {
            const int maxAttempts = 10;
            const float castFromAbove = 8f;   // how high above the candidate to start the ray
            const float castDistance = 16f;   // maximum downward ray distance
            const float checkRadius = 0.2f;   // sphere size for overlap check

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Random horizontal scatter
                Vector2 circle = UnityEngine.Random.insideUnitCircle * spawnRadius;
                Vector3 candidate = new Vector3(
                    areaCenter.x + circle.x,
                    areaCenter.y,
                    areaCenter.z + circle.y
                );

                // Cast downward to find the real floor under this point
                Vector3 rayOrigin = candidate + Vector3.up * castFromAbove;
                float floorY = areaCenter.y; // fallback: use area center height

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, castDistance))
                {
                    floorY = hit.point.y;
                }

                float height = UnityEngine.Random.Range(spawnMinHeight, spawnMaxHeight);
                Vector3 spawnPos = new Vector3(candidate.x, floorY + height, candidate.z);

                // Make sure the spot is clear of geometry
                if (!Physics.CheckSphere(spawnPos, checkRadius))
                {
                    return spawnPos;
                }
            }

            // All attempts failed — spawn straight above the area center
            Debug.LogWarning($"FlySwattingGameManager: Could not find clear spawn near {areaCenter}, using fallback.");
            return areaCenter + Vector3.up * spawnMinHeight;
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

        public void ResetGame()
        {
            _gameOver = false;
            _isCheckingForEmpty = false;
            _currentHearts = maxHearts;
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);

            // Clean up old slippers
            foreach (var slipper in _activeSlippers)
            {
                if (slipper != null)
                    Destroy(slipper.gameObject);
            }
            _activeSlippers.Clear();

            // Spawn flies
            SpawnFlies();

            // Spawn fresh slippers
            SpawnSlipperBatch(useInitialSpawn: true);

            Debug.Log("FlySwattingGameManager: Game reset.");
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
