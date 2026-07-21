using UnityEngine;
using Tsinelas.TumbangPreso;
using Tsinelas.Puno;
using Tsinelas.FlySwatting;
using Tsinelas.UI;

namespace Tsinelas
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Global Sounds")]
        public AudioClip winClip;
        public AudioClip loseClip;
        public AudioClip buttonClickClip;

        [Header("Fly Swatting")]
        public AudioClip flyHitClip;
        public AudioClip flyBuzzClip;

        [Header("Puno")]
        public AudioClip ballHitClip;

        [Header("Tumbang Preso")]
        public AudioClip canHitClip;
        public AudioClip canFallClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // No longer DontDestroyOnLoad, now per-scene
        }

        private void Start()
        {
            ConnectToAvailableItems();
        }

        private void ConnectToAvailableItems()
        {
            // 1. Tumbang Preso
            var tumbangGameManager = Object.FindFirstObjectByType<TumbangPresoGameManager>();
            if (tumbangGameManager != null)
            {
                var source = EnsureAudioSource(tumbangGameManager.gameObject, is2D: true);
                tumbangGameManager.OnGameWon += () => PlayClip(source, winClip);
                tumbangGameManager.OnGameLost += () => PlayClip(source, loseClip);
            }

            var can = Object.FindFirstObjectByType<TumbangPresoCan>();
            if (can != null)
            {
                var source = EnsureAudioSource(can.gameObject, is2D: false);
                can.OnCanHit += () => PlayClip(source, canHitClip);
                can.OnCanKnockedDown += () => PlayClip(source, canFallClip);
            }

            // 2. Puno
            var punoGameManager = Object.FindFirstObjectByType<PunoGameManager>();
            if (punoGameManager != null)
            {
                var source = EnsureAudioSource(punoGameManager.gameObject, is2D: true);
                punoGameManager.OnGameWon += () => PlayClip(source, winClip);
                punoGameManager.OnGameLost += () => PlayClip(source, loseClip);
            }

            var ball = Object.FindFirstObjectByType<PunoBall>();
            if (ball != null)
            {
                var source = EnsureAudioSource(ball.gameObject, is2D: false);
                ball.OnBallHit += () => PlayClip(source, ballHitClip);
            }

            // 3. Fly Swatting
            var flyGameManager = Object.FindFirstObjectByType<FlySwattingGameManager>();
            if (flyGameManager != null)
            {
                var source = EnsureAudioSource(flyGameManager.gameObject, is2D: true);
                flyGameManager.OnGameWon += () => PlayClip(source, winClip);
                flyGameManager.OnGameLost += () => PlayClip(source, loseClip);
                flyGameManager.OnFlySpawned += HandleFlySpawned;

                var existingFlies = Object.FindObjectsByType<FlyBehavior>(FindObjectsSortMode.None);
                foreach (var fly in existingFlies)
                {
                    HandleFlySpawned(fly);
                }
            }

            // 4. UI Tutorial Panels
            var tutorialPanels = Object.FindObjectsByType<TutorialPanel>(FindObjectsSortMode.None);
            foreach (var panel in tutorialPanels)
            {
                var source = EnsureAudioSource(panel.gameObject, is2D: true);
                panel.OnButtonClicked += () => PlayClip(source, buttonClickClip);
            }
        }

        private void HandleFlySpawned(FlyBehavior fly)
        {
            if (fly == null) return;
            
            fly.OnFlyDowned -= HandleFlyDowned;
            fly.OnFlyDowned += HandleFlyDowned;
            
            fly.SetBuzzClip(flyBuzzClip);
        }
        
        private void HandleFlyDowned(FlyBehavior fly)
        {
            if (fly != null)
            {
                var source = EnsureAudioSource(fly.gameObject, is2D: false);
                PlayClip(source, flyHitClip);
            }
        }

        private AudioSource EnsureAudioSource(GameObject go, bool is2D)
        {
            var source = go.GetComponent<AudioSource>();
            if (source == null)
            {
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            source.spatialBlend = is2D ? 0f : 1f;
            return source;
        }

        private void PlayClip(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }
    }
}
