using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    // Singleton � ����� MusicManager ���������� ����� �������
    public static MusicManager Instance { get; private set; }

    [Header("Menu Tracks")]
    [Tooltip("������ ������ ��� �������� ����, ������� ����� ������������� ����������.")]
    public AudioClip[] menuTracks;

    [Header("Level Tracks")]
    [Tooltip("������ ������ ��� ������� �������, ������� ����� ������������� ����������.")]
    public AudioClip[] levelTracks;

    [Header("Volume & Transition")]
    [Tooltip("���������� ��������� ������.")]
    public float normalVolume = 1f;
    [Tooltip("��������� ������ � ������ �����.")]
    public float pausedVolume = 0.2f;
    [Tooltip("����� ��� �������� �������� ����� ������� ��� ����� ������ (fade duration).")]
    public float fadeDuration = 0.5f;

    // ��������������� ����������
    private AudioSource audioSource;
    // ������� ����� ������ (�� menuTracks ��� levelTracks)
    private AudioClip[] currentTrackList;
    // ������� ������ � ������� ������
    private int currentTrackIndex = 0;
    // ������ �� �������� ������������ ������������
    private Coroutine cycleCoroutine = null;
    // ����, ����������� �� ��, ��� ������ � ������ ����� � ������������ ��� ������������� ���������
    private bool isPaused = false;

    void Awake()
    {
        // ���������� Singleton
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

        // �������� AudioSource, ���� ��������� ���, ���� ��� ��� ���
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // � ������ ������������ ������������ ���� �� ������ ������������� ����� ������
        audioSource.loop = false;
        audioSource.volume = normalVolume;

        // ������������� �� ������� �������� ����� �����
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // ��� ������� �������� ������ ����� ������ �� ����� ������� �����
        SetMusicForScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ������� �������� ����� �����
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetMusicForScene(scene);
    }

    /// <summary>
    /// �������� ����� ������ � ����������� �� �����.
    /// ���� ����� ���������� "MainMenu", ������������ ����, ����� � ������ ������.
    /// ��� ����� ������ ������ ���������� ������� �������.
    /// </summary>
    void SetMusicForScene(Scene scene)
    {
        AudioClip[] newTrackList = (scene.name == "MainMenu") ? menuTracks : levelTracks;

        // ���� ����� ����� ���������� �� ��������, ���� ���� ������ ���� ��� ������� ������ �� ������ � ���� � �������� ����� ����
        if (newTrackList == null || newTrackList.Length == 0)
        {
            Debug.LogWarning("Track list is empty for scene: " + scene.name);
            return;
        }

        // ���� �����TrackList ���������� ��� ���� ������� ������ �� �����������
        if (currentTrackList != newTrackList || audioSource.clip == null || System.Array.IndexOf(newTrackList, audioSource.clip) == -1)
        {
            // ������������� ���������� ����, ���� �� ����
            if (cycleCoroutine != null)
                StopCoroutine(cycleCoroutine);

            currentTrackList = newTrackList;
            currentTrackIndex = 0;
            // ������ ����������� ������ �� ������ ���� ������ ������
            StartCoroutine(SwitchMusic(currentTrackList[currentTrackIndex]));
            // ��������� ���� ������������ ������
            cycleCoroutine = StartCoroutine(CycleMusicCoroutine());
        }
    }

    /// <summary>
    /// ����������� ������������ ������ �� currentTrackList.
    /// ����� ��������� �������� ����� ����������� ������� ������� � ����������.
    /// </summary>
    IEnumerator CycleMusicCoroutine()
    {
        while (true)
        {
            if (audioSource.clip != null)
            {
                // ���, ���� ���������� ����� ����� ������ ������ fadeDuration
                float timeToWait = audioSource.clip.length - audioSource.time - fadeDuration;
                if (timeToWait > 0)
                    yield return new WaitForSeconds(timeToWait);
                else
                    yield return null;

                // ��������� � ���������� ����� � ������ �� �����
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
    /// ������� ������������ �� ����� ���� � �������������� fade-out � fade-in.
    /// </summary>
    IEnumerator SwitchMusic(AudioClip newClip)
    {
        // ��������� ������� ���������
        float startVolume = audioSource.volume;
        float timer = 0f;

        // ������� ��������� �������� �����
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeDuration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.clip = newClip;
        audioSource.Play();
        // ���� ������ ������ �� � ������ �����, �� ��������������� ���������� ���������, ����� � ��������� pausedVolume
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
    /// ���������� ����� ��� ��������� ������ �����.
    /// ��� ���� ��������� ������ ������ ����������� ��� �������������.
    /// </summary>
    /// <param name="paused">���� true � ���� �� �����.</param>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        if (cycleCoroutine != null)
            StopCoroutine(cycleCoroutine);
        StopAllCoroutines(); // ��������� ������� �����, ����� �� ���� ����������.
        StartCoroutine(FadeVolume(paused ? pausedVolume : normalVolume, fadeDuration));
        // ����� ��������� ��������� ����� ������������� ����, ���� �����.
        if (currentTrackList != null && currentTrackList.Length > 0)
        {
            cycleCoroutine = StartCoroutine(CycleMusicCoroutine());
        }
    }

    /// <summary>
    /// ������� ��������� ��������� AudioSource �� targetVolume �� ����� duration.
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
