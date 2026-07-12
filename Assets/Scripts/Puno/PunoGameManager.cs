using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tsinelas.TumbangPreso;

namespace Tsinelas.Puno
{
    public class PunoGameManager : MonoBehaviour
    {
        [Header("Slipper Spawning")]
        public GameObject slipperPrefab;
        public int slippersPerBatch = 2;
        public Vector3 initialSpawnOffset = new Vector3(0f, 0.05f, 0f);
        public Vector3 respawnOffset = new Vector3(0f, 1.2f, 0.6f);
        public float respawnSpread = 0.3f;

        [Header("Slipper Detection")]
        public float respawnDelay = 1.0f;

        [Header("References")]
        [Tooltip("The ball object with the PunoBall script in the scene.")]
        public PunoBall ball;

        [Header("Win / Lose")]
        public int maxHearts = 3;
        public GameObject winPanelPrefab;
        public GameObject losePanelPrefab;

        public event Action<int, int> OnHeartsChanged;

        private List<TumbangPresoSlipper> _activeSlippers = new List<TumbangPresoSlipper>();
        private Transform _playerTransform;
        private bool _isCheckingForEmpty = false;
        private bool _gameOver = false;
        private int _currentHearts;

        private void Start()
        {
            if (Camera.main != null)
                _playerTransform = Camera.main.transform;

            if (slipperPrefab == null)
            {
                Debug.LogError("PunoGameManager: Slipper Prefab is not assigned!");
                return;
            }

            if (ball == null)
            {
                Debug.LogError("PunoGameManager: PunoBall reference is not assigned!");
                return;
            }

            ball.OnBallFreed += HandleBallFreed;

            _currentHearts = maxHearts;
            OnHeartsChanged?.Invoke(_currentHearts, maxHearts);

            SpawnSlipperBatch(useInitialSpawn: true);
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
            Debug.Log($"PunoGameManager: Heart lost. Hearts remaining: {_currentHearts}/{maxHearts}");
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
                    Debug.LogWarning("PunoGameManager: Spawned slipper prefab is missing TumbangPresoSlipper component.");
                }
            }

            Debug.Log($"PunoGameManager: Spawned {slippersPerBatch} slipper(s). UseInitialSpawn={useInitialSpawn}");
        }

        private void HandleBallFreed()
        {
            if (_gameOver) return;
            _gameOver = true;

            Debug.Log("PunoGameManager: BALL FREED! Player wins!");
            ShowWin();
        }

        private void ShowWin()
        {
            if (winPanelPrefab == null)
            {
                Debug.LogWarning("PunoGameManager: Win Panel Prefab is not assigned.");
                return;
            }
            Instantiate(winPanelPrefab);
        }

        private void ShowLose()
        {
            _gameOver = true;
            Debug.Log("PunoGameManager: Out of hearts! Player loses.");

            if (losePanelPrefab == null)
            {
                Debug.LogWarning("PunoGameManager: Lose Panel Prefab is not assigned.");
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
            Debug.Log($"PunoGameManager: Hearts restored to {maxHearts}.");

            foreach (var slipper in _activeSlippers)
            {
                if (slipper != null) Destroy(slipper.gameObject);
            }
            _activeSlippers.Clear();
            SpawnSlipperBatch(useInitialSpawn: true);
            Debug.Log("PunoGameManager: Game reset.");
        }

        private void OnDestroy()
        {
            if (ball != null)
                ball.OnBallFreed -= HandleBallFreed;
        }
    }
}
