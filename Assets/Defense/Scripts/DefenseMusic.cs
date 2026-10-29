using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronKeep
{
    // Background music that survives scene loads and cross-fades to the track for each scene.
    // Tracks live in Assets/Defense/Resources/Music. M toggles mute (remembered in PlayerPrefs).
    public class DefenseMusic : MonoBehaviour
    {
        const string MuteKey = "IronKeepMusicMuted";
        const float Volume = .35f, FadeSeconds = 1.2f;
        static DefenseMusic instance;
        AudioSource current, previous;
        bool muted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("Defense Music"); DontDestroyOnLoad(go);
            instance = go.AddComponent<DefenseMusic>();
        }
        void Awake()
        {
            muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            current = NewSource(); previous = NewSource();
            SceneManager.activeSceneChanged += (_, scene) => Play(scene.name);
            Play(SceneManager.GetActiveScene().name);
        }
        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0; s.ignoreListenerPause = true;
            return s;
        }
        static string TrackFor(string scene) =>
            scene == "Main" ? "Music/Battle" : scene == "Victory" ? "Music/Victory" : scene == "Defeat" ? "Music/Defeat" : "Music/Lobby";
        void Play(string scene)
        {
            var clip = Resources.Load<AudioClip>(TrackFor(scene));
            if (!clip || current.clip == clip) return;
            (previous, current) = (current, previous);
            current.clip = clip; current.volume = 0; current.Play();
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                muted = !muted; PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0); PlayerPrefs.Save();
                if (DefenseGame.Instance) DefenseGame.Instance.Message(muted ? "배경음악 끔 / M" : "배경음악 켬 / M");
            }
            float step = Time.unscaledDeltaTime / FadeSeconds * Volume;
            current.volume = Mathf.MoveTowards(current.volume, muted ? 0 : Volume, step);
            previous.volume = Mathf.MoveTowards(previous.volume, 0, step);
            if (previous.isPlaying && previous.volume <= 0) previous.Stop();
        }
    }
}
