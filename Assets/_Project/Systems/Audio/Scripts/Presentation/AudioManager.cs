using UnityEngine;
using Runefall.Core;

namespace Runefall.Audio
{
    /// <summary>
    /// Implementación de IAudioService. Mantiene un pool de AudioSources round-robin para
    /// reproducir SFX/voces one-shot sin instanciar GameObjects por sonido.
    ///
    /// Se registra solo en ServiceLocator&lt;IAudioService&gt; en Awake. Si ningún consumidor
    /// lo encuentra en escena, GetOrCreate() crea uno persistente bajo demanda.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour, IAudioService
    {
        [Header("Pool")]
        [Tooltip("Cantidad de AudioSources simultáneos. Sonidos extra reutilizan el más antiguo.")]
        [SerializeField] private int _voiceCount = 12;

        [Header("Espacialización")]
        [Tooltip("0 = 2D (siempre audible), 1 = 3D posicional. Voces de combate: 0 recomendado.")]
        [Range(0f, 1f)] [SerializeField] private float _spatialBlend = 0f;

        [Header("Volumen")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;

        private AudioSource[] _pool;
        private int _next;

        private static AudioManager _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            BuildPool();
            ServiceLocator.Register<IAudioService>(this);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                if (ServiceLocator.TryGet<IAudioService>(out var svc) && ReferenceEquals(svc, this))
                    ServiceLocator.Unregister<IAudioService>();
                _instance = null;
            }
        }

        private void BuildPool()
        {
            if (_pool != null) return;
            _voiceCount = Mathf.Max(1, _voiceCount);
            _pool = new AudioSource[_voiceCount];
            for (int i = 0; i < _voiceCount; i++)
            {
                var src = new GameObject($"Voice_{i}").AddComponent<AudioSource>();
                src.transform.SetParent(transform, false);
                src.playOnAwake = false;
                src.spatialBlend = _spatialBlend;
                _pool[i] = src;
            }
        }

        // ── IAudioService ─────────────────────────────────────────────────────────

        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var src = NextSource();
            if (src == null || clip == null) return;
            src.transform.position = position;
            Fire(src, clip, volume, pitch);
        }

        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            var src = NextSource();
            if (src == null || clip == null) return;
            Fire(src, clip, volume, pitch);
        }

        private void Fire(AudioSource src, AudioClip clip, float volume, float pitch)
        {
            src.clip   = clip;
            src.volume = Mathf.Clamp01(volume) * _masterVolume;
            src.pitch  = pitch <= 0f ? 1f : pitch;
            src.Play();
        }

        private AudioSource NextSource()
        {
            if (_pool == null) BuildPool();
            if (_pool == null || _pool.Length == 0) return null;

            // Prefer a free source; otherwise steal the round-robin slot.
            for (int i = 0; i < _pool.Length; i++)
            {
                int idx = (_next + i) % _pool.Length;
                if (_pool[idx] != null && !_pool[idx].isPlaying)
                {
                    _next = (idx + 1) % _pool.Length;
                    return _pool[idx];
                }
            }
            var s = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            return s;
        }

        /// <summary>
        /// Devuelve el servicio de audio registrado, creando un AudioManager persistente
        /// si todavía no existe. Permite que el audio funcione sin tenerlo cableado en escena.
        /// </summary>
        public static IAudioService GetOrCreate()
        {
            if (ServiceLocator.TryGet<IAudioService>(out var svc) && svc != null)
                return svc;

            var go = new GameObject("AudioManager (auto)");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>();
        }
    }
}
