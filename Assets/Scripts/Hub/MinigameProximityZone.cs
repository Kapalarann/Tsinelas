using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Tsinelas.Hub
{
    /// <summary>
    /// A proximity-based transition billboard. Displays a floating "!" within a World Space Canvas.
    /// When the player is close, the "!" hides and a UI Panel is revealed with a button to load the minigame scene.
    /// Uses TextMesh Pro for high-fidelity text rendering in VR.
    /// </summary>
    public class MinigameProximityZone : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [Tooltip("The name of the scene to load when the Play button is clicked.")]
        public string sceneToLoad;

        [Header("Billboard Text Content")]
        public string minigameTitle = "Minigame Title";
        public string minigameDescription = "Step closer and press Play to start!";

        [Header("Proximity Settings")]
        [Tooltip("Distance at which the Panel becomes visible and the exclamation mark hides.")]
        public float activationRange = 6.0f;

        [Header("Canvas References (Optional)")]
        [Tooltip("Custom Canvas. Generated automatically at runtime if left empty.")]
        public Canvas targetCanvas;
        [Tooltip("Custom TextMeshPro for the Exclamation Mark. Generated automatically at runtime if left empty.")]
        public TextMeshProUGUI exclamationText;
        [Tooltip("Custom Panel containing the minigame UI. Generated automatically at runtime if left empty.")]
        public GameObject billboardPanel;
        [Tooltip("Custom Button to trigger level load. Generated automatically at runtime if left empty.")]
        public Button playButton;

        [Header("Animation Settings")]
        public float bobSpeed = 2.0f;
        public float bobHeight = 15.0f; // Canvas space height offset

        private Transform _playerCamera;
        private float _exclamationStartLocalY;
        private bool _isTransitioning = false;

        private void Start()
        {
            // Try to find the Main Camera (VR Headset position)
            if (Camera.main != null)
            {
                _playerCamera = Camera.main.transform;
            }

            // Build the Canvas hierarchy dynamically if not assigned
            if (targetCanvas == null)
            {
                CreateDefaultCanvasHierarchy();
            }
            else
            {
                // If the user already assigned a Canvas but left elements unassigned, try to find them
                if (exclamationText == null) exclamationText = targetCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
                if (billboardPanel == null)
                {
                    Transform panelTransform = targetCanvas.transform.Find("BillboardPanel");
                    if (panelTransform != null) billboardPanel = panelTransform.gameObject;
                }
                if (playButton == null && billboardPanel != null)
                {
                    playButton = billboardPanel.GetComponentInChildren<Button>(true);
                }
            }

            // Setup button click listener
            if (playButton != null)
            {
                playButton.onClick.AddListener(TriggerSceneLoad);
            }
            else
            {
                Debug.LogError($"MinigameProximityZone on {gameObject.name}: Play Button reference is missing and could not be generated.");
            }

            if (exclamationText != null)
            {
                _exclamationStartLocalY = exclamationText.transform.localPosition.y;
            }

            // Ensure Canvas is ready for VR interaction
            if (targetCanvas != null)
            {
                GraphicRaycaster oldRaycaster = targetCanvas.GetComponent<GraphicRaycaster>();
                if (oldRaycaster != null && oldRaycaster.GetType() == typeof(GraphicRaycaster))
                {
                    Destroy(oldRaycaster);
                }
                if (targetCanvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
                {
                    targetCanvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                }
            }
        }

        private void Update()
        {
            // Dynamically locate camera if needed
            if (_playerCamera == null)
            {
                if (Camera.main != null)
                {
                    _playerCamera = Camera.main.transform;
                }
                else
                {
                    return;
                }
            }

            float distance = Vector3.Distance(transform.position, _playerCamera.position);

            // Rotate the entire Canvas to face the player headset
            if (targetCanvas != null)
            {
                Vector3 direction = _playerCamera.position - targetCanvas.transform.position;
                direction.y = 0; // Lock to horizontal plane so it doesn't tilt vertically
                if (direction != Vector3.zero)
                {
                    targetCanvas.transform.rotation = Quaternion.LookRotation(-direction);
                }
            }

            if (distance <= activationRange)
            {
                // Player is close: Hide exclamation text, show panel
                if (exclamationText != null) exclamationText.gameObject.SetActive(false);
                if (billboardPanel != null) billboardPanel.SetActive(true);
            }
            else
            {
                // Player is far: Show exclamation text, hide panel
                if (exclamationText != null)
                {
                    exclamationText.gameObject.SetActive(true);
                    AnimateExclamation();
                }
                if (billboardPanel != null) billboardPanel.SetActive(false);
            }
        }

        private void AnimateExclamation()
        {
            if (exclamationText == null) return;

            // Bobbing the exclamation mark up and down in local Canvas space
            float newY = _exclamationStartLocalY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            Vector3 pos = exclamationText.transform.localPosition;
            exclamationText.transform.localPosition = new Vector3(pos.x, newY, pos.z);
        }

        private void TriggerSceneLoad()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log($"MinigameProximityZone: Button clicked! Loading scene: {sceneToLoad}");
            SceneManager.LoadScene(sceneToLoad);
        }

        #region Runtime UI Generation

        private void CreateDefaultCanvasHierarchy()
        {
            // 1. Create Canvas
            GameObject canvasGo = new GameObject("ProximityCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0, 1.5f, 0); // 1.5m off the ground

            targetCanvas = canvasGo.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(400, 300); // 400x300 UI pixels
            canvasRect.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Scale down so it measures ~2m wide in world space

            // 2. Create Floating "!"
            GameObject exclamationGo = new GameObject("ExclamationText");
            exclamationGo.transform.SetParent(canvasGo.transform, false);
            exclamationText = exclamationGo.AddComponent<TextMeshProUGUI>();
            exclamationText.text = "!";
            exclamationText.fontSize = 120;
            exclamationText.color = Color.yellow;
            exclamationText.alignment = TextAlignmentOptions.Center;

            RectTransform exclRect = exclamationGo.GetComponent<RectTransform>();
            exclRect.sizeDelta = new Vector2(100, 150);
            exclRect.localPosition = Vector3.zero;

            // 3. Create Billboard Panel
            billboardPanel = new GameObject("BillboardPanel");
            billboardPanel.transform.SetParent(canvasGo.transform, false);
            Image panelImage = billboardPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Dark background

            RectTransform panelRect = billboardPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero; // Stretch to fill Canvas

            // 3a. Panel Title
            GameObject titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(billboardPanel.transform, false);
            TextMeshProUGUI titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = minigameTitle;
            titleText.fontSize = 32;
            titleText.color = Color.cyan;
            titleText.alignment = TextAlignmentOptions.Center;

            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.7f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = Vector2.zero;

            // 3b. Panel Description
            GameObject descGo = new GameObject("DescriptionText");
            descGo.transform.SetParent(billboardPanel.transform, false);
            TextMeshProUGUI descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = minigameDescription;
            descText.fontSize = 18;
            descText.color = Color.white;
            descText.alignment = TextAlignmentOptions.Center;

            RectTransform descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.05f, 0.35f);
            descRect.anchorMax = new Vector2(0.95f, 0.65f);
            descRect.sizeDelta = Vector2.zero;

            // 3c. Play Button
            GameObject buttonGo = new GameObject("PlayButton");
            buttonGo.transform.SetParent(billboardPanel.transform, false);
            Image btnImg = buttonGo.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.6f, 0.15f, 1f); // Green button

            playButton = buttonGo.AddComponent<Button>();

            RectTransform btnRect = buttonGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.3f, 0.08f);
            btnRect.anchorMax = new Vector2(0.7f, 0.28f);
            btnRect.sizeDelta = Vector2.zero;

            // Button Text
            GameObject btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(buttonGo.transform, false);
            TextMeshProUGUI btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnText.text = "PLAY";
            btnText.fontSize = 20;
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;

            RectTransform btnTextRect = btnTextGo.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;

            // Hide Panel by default
            billboardPanel.SetActive(false);
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationRange);
        }
    }
}
