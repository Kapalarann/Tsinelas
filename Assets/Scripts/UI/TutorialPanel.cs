using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Tsinelas.UI
{
    /// <summary>
    /// An adjustable tutorial panel that cycles through multiple separate GameObjects as pages.
    /// It automatically positions itself in front of the VR player's camera and acts as a billboard.
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private List<Button> _nextButtons = new List<Button>();
        [SerializeField] private List<Button> _prevButtons = new List<Button>();
        [SerializeField] private List<Button> _closeButtons = new List<Button>();

        [Header("Tutorial Panels")]
        [Tooltip("Assign the different panel GameObjects that represent each tutorial page.")]
        [SerializeField] private List<GameObject> _panels = new List<GameObject>();

        [Header("Positioning")]
        [Tooltip("Distance in front of the player (metres) where the panel appears.")]
        [SerializeField] private float _spawnDistance = 1.5f;

        [Tooltip("Vertical offset relative to the player camera when spawning.")]
        [SerializeField] private float _heightOffset = 0f;

        [Tooltip("Horizontal offset relative to the player camera (positive = right, negative = left).")]
        [SerializeField] private float _sideOffset = 0f;

        private int _currentPageIndex = 0;
        private Transform _playerCamera;

        public event System.Action OnButtonClicked;

        private void Start()
        {
            _playerCamera = Camera.main != null ? Camera.main.transform : null;
            
            PositionInFrontOfPlayer();

            foreach (var btn in _nextButtons) { if (btn != null) btn.onClick.AddListener(NextPage); }
            foreach (var btn in _prevButtons) { if (btn != null) btn.onClick.AddListener(PrevPage); }
            foreach (var btn in _closeButtons) { if (btn != null) btn.onClick.AddListener(ClosePanel); }

            EnsureVRCanvas();
            UpdatePageDisplay();
        }

        private void Update()
        {
            // Always face the player camera
            if (_playerCamera == null) return;

            Vector3 direction = _playerCamera.position - transform.position;
            direction.y = 0f;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(-direction);
        }

        private void PositionInFrontOfPlayer()
        {
            if (_playerCamera == null) return;

            Vector3 forward = new Vector3(_playerCamera.forward.x, 0f, _playerCamera.forward.z).normalized;
            
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

        private void EnsureVRCanvas()
        {
            // Ensure Canvas is ready for VR interaction
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                GraphicRaycaster oldRaycaster = canvas.GetComponent<GraphicRaycaster>();
                if (oldRaycaster != null && oldRaycaster.GetType() == typeof(GraphicRaycaster))
                {
                    Destroy(oldRaycaster);
                }
                
                // Add VR Raycaster if not present
                if (canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                }
            }
        }

        /// <summary>
        /// Dynamically set the tutorial panels and reset to the first panel.
        /// </summary>
        public void SetPanels(List<GameObject> panels)
        {
            _panels = panels;
            _currentPageIndex = 0;
            UpdatePageDisplay();
        }

        private void NextPage()
        {
            OnButtonClicked?.Invoke();
            if (_currentPageIndex < _panels.Count - 1)
            {
                _currentPageIndex++;
                UpdatePageDisplay();
            }
        }

        private void PrevPage()
        {
            OnButtonClicked?.Invoke();
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                UpdatePageDisplay();
            }
        }

        private void UpdatePageDisplay()
        {
            if (_panels == null || _panels.Count == 0) return;

            // Enable only the current panel, disable all others
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i] != null)
                {
                    _panels[i].SetActive(i == _currentPageIndex);
                }
            }

            // Update button visibility based on the current page
            foreach (var btn in _prevButtons)
            {
                if (btn != null) btn.gameObject.SetActive(_currentPageIndex > 0);
            }
                
            foreach (var btn in _nextButtons)
            {
                if (btn != null) btn.gameObject.SetActive(_currentPageIndex < _panels.Count - 1);
            }
                
            foreach (var btn in _closeButtons)
            {
                if (btn != null) btn.gameObject.SetActive(_currentPageIndex == _panels.Count - 1 || _panels.Count <= 1);
            }
        }

        private void ClosePanel()
        {
            OnButtonClicked?.Invoke();
            Destroy(gameObject, 0.1f);
        }
        
        private void OnDestroy()
        {
            foreach (var btn in _nextButtons) { if (btn != null) btn.onClick.RemoveListener(NextPage); }
            foreach (var btn in _prevButtons) { if (btn != null) btn.onClick.RemoveListener(PrevPage); }
            foreach (var btn in _closeButtons) { if (btn != null) btn.onClick.RemoveListener(ClosePanel); }
        }
    }
}
