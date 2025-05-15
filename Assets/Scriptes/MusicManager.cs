using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    // Singleton – чтобы MusicManager сохранялся между сценами
    public static MusicManager Instance { get; private set; }

    [Header("Menu Tracks")]
    [Tooltip("Массив треков для главного меню, которые будут проигрываться циклически.")]
    public AudioClip[] menuTracks;

    [Header("Level Tracks")]
    [Tooltip("Массив треков для игровых уровней, которые будут проигрываться циклически.")]
    public AudioClip[] levelTracks;

    [Header("Volume & Transition")]
    [Tooltip("Нормальная громкость музыки.")]
    public float normalVolume = 1f;
    [Tooltip("Громкость музыки в режиме паузы.")]
    public float pausedVolume = 0.2f;
    [Tooltip("Время для плавного перехода между треками или смены музыки (fade duration).")]
    public float fadeDuration = 0.5f;

    // Вспомогательные переменные
    private AudioSource audioSource;
    // Текущий набор треков (из menuTracks или levelTracks)
    private AudioClip[] currentTrackList;
    // Текущий индекс в массиве треков
    private int currentTrackIndex = 0;
    // Ссылка на корутину циклического проигрывания
    private Coroutine cycleCoroutine = null;
    // Флаг, указывающий на то, что музыка в режиме паузы – используется для регулирования громкости
    private bool isPaused = false;

    void Awake()
    {
        // Реализация Singleton
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

        // Получаем AudioSource, либо добавляем его, если его ещё нет
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // В режиме циклического проигрывания трек не должен зацикливаться самим клипом
        audioSource.loop = false;
        audioSource.volume = normalVolume;

        // Подписываемся на событие загрузки новой сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // При запуске выбираем нужный набор треков по имени текущей сцены
        SetMusicForScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Событие загрузки новой сцены
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetMusicForScene(scene);
    }

    /// <summary>
    /// Выбираем набор треков в зависимости от сцены.
    /// Если сцена называется "MainMenu", используется меню, иначе – музыка уровня.
    /// При смене набора треков происходит плавный переход.
    /// </summary>
    void SetMusicForScene(Scene scene)
    {
        AudioClip[] newTrackList = (scene.name == "MainMenu") ? menuTracks : levelTracks;

        // Если новый набор отличается от текущего, либо если список пуст или текущая музыка не входит в него – начинаем новый цикл
        if (newTrackList == null || newTrackList.Length == 0)
        {
            Debug.LogWarning("Track list is empty for scene: " + scene.name);
            return;
        }

        // Если текущTrackList отличается или если текущая музыка не содердается
        if (currentTrackList != newTrackList || audioSource.clip == null || System.Array.IndexOf(newTrackList, audioSource.clip) == -1)
        {
            // Останавливаем предыдущий цикл, если он есть
            if (cycleCoroutine != null)
                StopCoroutine(cycleCoroutine);

            currentTrackList = newTrackList;
            currentTrackIndex = 0;
            // Плавно переключаем музыку на первый трек нового набора
            StartCoroutine(SwitchMusic(currentTrackList[currentTrackIndex]));
            // Запускаем цикл проигрывания треков
            cycleCoroutine = StartCoroutine(CycleMusicCoroutine());
        }
    }

    /// <summary>
    /// Циклическое проигрывание треков из currentTrackList.
    /// После окончания текущего трека запускается плавный переход к следующему.
    /// </summary>
    IEnumerator CycleMusicCoroutine()
    {
        while (true)
        {
            if (audioSource.clip != null)
            {
                // Ждём, пока оставшееся время трека станет меньше fadeDuration
                float timeToWait = audioSource.clip.length - audioSource.time - fadeDuration;
                if (timeToWait > 0)
                    yield return new WaitForSeconds(timeToWait);
                else
                    yield return null;

                // Переходим к следующему треку в списке по циклу
                currentTrackIndex = (currentTrackIndex + 1) % currentTrackList.Length;
                yield return StartCoroutine(SwitchMusic(currentTrackList[currentTrackIndex]));
            }
            else
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// Плавное переключение на новый трек с использованием fade-out и fade-in.
    /// </summary>
    IEnumerator SwitchMusic(AudioClip newClip)
    {
        // Сохраняем текущую громкость
        float startVolume = audioSource.volume;
        float timer = 0f;

        // Плавное затухание текущего трека
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeDuration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.clip = newClip;
        audioSource.Play();
        // Если музыка сейчас не в режиме паузы, то восстанавливаем нормальную громкость, иначе – оставляем pausedVolume
        float targetVol = isPaused ? pausedVolume : normalVolume;
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVol, timer / fadeDuration);
            yield return null;
        }
        audioSource.volume = targetVol;
    }

    /// <summary>
    /// Вызывается извне для установки режима паузы.
    /// При этом громкость музыки плавно уменьшается или увеличивается.
    /// </summary>
    /// <param name="paused">Если true – игра на паузе.</param>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        if (cycleCoroutine != null)
            StopCoroutine(cycleCoroutine);
        StopAllCoroutines(); // Прерываем текущие фейды, чтобы не было конфликтов.
        StartCoroutine(FadeVolume(paused ? pausedVolume : normalVolume, fadeDuration));
        // После изменения громкости можно перезапустить цикл, если нужно.
        if (currentTrackList != null && currentTrackList.Length > 0)
        {
            cycleCoroutine = StartCoroutine(CycleMusicCoroutine());
        }
    }

    /// <summary>
    /// Плавное изменение громкости AudioSource до targetVolume за время duration.
    /// </summary>
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
