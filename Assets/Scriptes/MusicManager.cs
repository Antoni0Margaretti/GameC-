using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    // Singleton-экземпляр, чтобы этот объект не уничтожался при загрузке новых сцен.
    public static MusicManager Instance { get; private set; }

    [Header("Music Clips")]
    [Tooltip("Музыкальный трек главного меню.")]
    public AudioClip mainMenuMusic;
    [Tooltip("Музыкальный трек для игровых уровней.")]
    public AudioClip levelMusic;

    [Header("Settings")]
    [Tooltip("Нормальная громкость музыки.")]
    public float normalVolume = 1f;
    [Tooltip("Громкость музыки в режиме паузы.")]
    public float pausedVolume = 0.2f;
    [Tooltip("Время на плавное увеличение/уменьшение громкости.")]
    public float fadeDuration = 0.5f;

    private AudioSource audioSource;

    void Awake()
    {
        // Реализуем паттерн Singleton.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.loop = true;
        audioSource.volume = normalVolume;

        // Подписываемся на событие загрузки новой сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // При запуске игры выбираем трек для текущей сцены.
        SetMusicForScene(SceneManager.GetActiveScene());
    }

    // При загрузке новой сцены вызывается эта функция
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetMusicForScene(scene);
    }

    // Выбираем трек в зависимости от имени загруженной сцены.
    // Предполагается, что сцена главного меню называется "MainMenu".
    void SetMusicForScene(Scene scene)
    {
        AudioClip clipToPlay = (scene.name == "MainMenu") ? mainMenuMusic : levelMusic;
        if (audioSource.clip != clipToPlay)
        {
            // Производим плавное переключение музыки.
            StartCoroutine(SwitchMusic(clipToPlay));
        }
    }

    // Плавное переключение музыки с fade-out старого трека и fade-in нового.
    IEnumerator SwitchMusic(AudioClip newClip)
    {
        float startVolume = audioSource.volume;
        float t = 0f;
        // Плавное затухание (fade-out)
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.clip = newClip;
        audioSource.Play();
        t = 0f;
        // Плавное увеличение громкости (fade-in)
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(0f, normalVolume, t / fadeDuration);
            yield return null;
        }
        audioSource.volume = normalVolume;
    }

    /// <summary>
    /// Вызывайте этот метод из других скриптов (например, при открытии/закрытии меню паузы),
    /// чтобы изменить громкость музыки.
    /// </summary>
    /// <param name="paused">Если true – игра на паузе, звук приглушается.</param>
    public void SetPaused(bool paused)
    {
        StopAllCoroutines(); // Прерываем любые текущие изменения громкости, чтобы избежать конфликтов.
        StartCoroutine(FadeVolume(paused ? pausedVolume : normalVolume, fadeDuration));
    }

    // Плавное изменение громкости до targetVolume за время duration.
    IEnumerator FadeVolume(float targetVolume, float duration)
    {
        float startVolume = audioSource.volume;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
            yield return null;
        }
        audioSource.volume = targetVolume;
    }
}