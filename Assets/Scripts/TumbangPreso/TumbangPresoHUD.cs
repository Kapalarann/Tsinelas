using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tsinelas.TumbangPreso
{
    /// <summary>
    /// Connects the TumbangPresoGameManager's heart system to the UI.
    /// Can display hearts as either text (e.g. "Hearts: 3") or via an array of UI Images.
    /// </summary>
    public class TumbangPresoHUD : MonoBehaviour
    {
        [Header("Manager Reference")]
        [Tooltip("Reference to the GameManager. If left empty, it will try to find it in the scene automatically.")]
        [SerializeField] private TumbangPresoGameManager _gameManager;

        [Header("UI Elements")]
        [Tooltip("(Optional) Text component to display remaining hearts.")]
        [SerializeField] private TextMeshProUGUI _heartsText;

        [Tooltip("(Optional) Array of UI Images representing hearts. Element 0 is heart 1, etc.")]
        [SerializeField] private Image[] _heartImages;

        private void Start()
        {
            // Auto-find GameManager if not assigned
            if (_gameManager == null)
            {
                _gameManager = Object.FindObjectOfType<TumbangPresoGameManager>();
            }

            if (_gameManager != null)
            {
                // Subscribe to the event
                _gameManager.OnHeartsChanged += UpdateUI;
            }
            else
            {
                Debug.LogWarning("TumbangPresoHUD: Could not find TumbangPresoGameManager in the scene.");
            }
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                // Always unsubscribe to prevent memory leaks!
                _gameManager.OnHeartsChanged -= UpdateUI;
            }
        }

        /// <summary>
        /// Called automatically by the GameManager when the heart count changes.
        /// </summary>
        private void UpdateUI(int currentHearts, int maxHearts)
        {
            // 1. Update Text (if assigned)
            if (_heartsText != null)
            {
                _heartsText.text = $"Hearts: {currentHearts} / {maxHearts}";
            }

            // 2. Update Images (if assigned)
            if (_heartImages != null && _heartImages.Length > 0)
            {
                for (int i = 0; i < _heartImages.Length; i++)
                {
                    if (_heartImages[i] != null)
                    {
                        // Enable the image if its index is less than the current hearts,
                        // meaning if currentHearts is 2, indices 0 and 1 are active, and 2 is disabled.
                        _heartImages[i].enabled = (i < currentHearts);
                    }
                }
            }
        }
    }
}
