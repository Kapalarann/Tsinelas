using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Tsinelas.TumbangPreso
{
    /// <summary>
    /// Self-contained lose screen. Attach to your Lose Panel prefab and wire up the
    /// Retry and Hub buttons in the Inspector. The panel positions itself in front of
    /// the player when it spawns and always faces them.
    ///
    /// The GameManager only needs to Instantiate this prefab — no further coupling required.
    /// </summary>
    public class TumbangPresoLosePanel : MonoBehaviour
    {
        [Header("Button References")]
        [Tooltip("Button that retries the minigame (resets can, hearts, and slippers).")]
        [SerializeField] private Button _retryButton;

        [Tooltip("Button that returns the player to the hub scene.")]
        [SerializeField] private Button _hubButton;

        [Header("Scene Settings")]
        [Tooltip("Exact scene name as it appears in Build Settings.")]
        [SerializeField] private string _hubSceneName = "main hub";

        [Header("Positioning")]
        [Tooltip("Distance in front of the player (metres) where the panel appears.")]
        [SerializeField] private float _spawnDistance = 1.5f;

        [Tooltip("Vertical offset relative to the player camera when spawning.")]
        [SerializeField] private float _heightOffset = -0.2f;

        [Tooltip("Horizontal offset relative to the player camera (positive = right, negative = left).")]
        [SerializeField] private float _sideOffset = 0f;

        private Transform _playerCamera;
        private bool _isTransitioning = false;

        private void Start()
        {
            _playerCamera = Camera.main != null ? Camera.main.transform : null;

            PositionInFrontOfPlayer();

            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRetryClicked);
            else
                Debug.LogWarning("TumbangPresoLosePanel: Retry Button is not assigned.");

            if (_hubButton != null)
                _hubButton.onClick.AddListener(OnHubClicked);
            else
                Debug.LogWarning("TumbangPresoLosePanel: Hub Button is not assigned.");

            // Ensure Canvas is ready for VR interaction
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                GraphicRaycaster oldRaycaster = canvas.GetComponent<GraphicRaycaster>();
                if (oldRaycaster != null && oldRaycaster.GetType() == typeof(GraphicRaycaster))
                {
                    Destroy(oldRaycaster);
                }
                if (canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                }
            }
        }

        private void Update()
        {
            // Always face the player
            if (_playerCamera == null) return;

            Vector3 direction = _playerCamera.position - transform.position;
            direction.y = 0f;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(-direction);
        }

        private void PositionInFrontOfPlayer()
        {
            if (_playerCamera == null) return;

            Vector3 forward = new Vector3(
                _playerCamera.forward.x,
                0f,
                _playerCamera.forward.z
            ).normalized;

            transform.position = _playerCamera.position
                + forward * _spawnDistance
                + Vector3.up * _heightOffset
                + _playerCamera.right * _sideOffset;

            // Face the player immediately
            Vector3 dir = _playerCamera.position - transform.position;
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(-dir);
        }

        private void OnRetryClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log("TumbangPresoLosePanel: Retry clicked.");

            TumbangPresoGameManager manager = Object.FindFirstObjectByType<TumbangPresoGameManager>();
            Destroy(gameObject);

            if (manager != null)
                manager.ResetGame();
            else
                Debug.LogWarning("TumbangPresoLosePanel: Could not find TumbangPresoGameManager in scene.");
        }

        private void OnHubClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log($"TumbangPresoLosePanel: Back to Hub clicked. Loading '{_hubSceneName}'.");
            SceneManager.LoadScene(_hubSceneName);
        }

        private void OnDestroy()
        {
            if (_retryButton != null) _retryButton.onClick.RemoveListener(OnRetryClicked);
            if (_hubButton != null)   _hubButton.onClick.RemoveListener(OnHubClicked);
        }
    }
}
