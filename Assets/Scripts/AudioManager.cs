using UnityEngine;

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

        private AudioSource _2dAudioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _2dAudioSource = gameObject.AddComponent<AudioSource>();
            _2dAudioSource.spatialBlend = 0f; // Force 2D
        }

        // --- 2D Sound Helpers ---
        public void PlayWinSound() => Play2D(winClip);
        public void PlayLoseSound() => Play2D(loseClip);
        public void PlayButtonClick() => Play2D(buttonClickClip);

        private void Play2D(AudioClip clip)
        {
            if (clip != null && _2dAudioSource != null)
                _2dAudioSource.PlayOneShot(clip);
        }

        // --- 3D Sound Helpers ---
        public void PlayFlyHit(Vector3 position) => Play3D(flyHitClip, position);
        public void PlayBallHit(Vector3 position) => Play3D(ballHitClip, position);
        public void PlayCanHit(Vector3 position) => Play3D(canHitClip, position);
        public void PlayCanFall(Vector3 position) => Play3D(canFallClip, position);

        private void Play3D(AudioClip clip, Vector3 position)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position);
            }
        }
    }
}
