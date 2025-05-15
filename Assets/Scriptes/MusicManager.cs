using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    // Singleton-���������, ����� ���� ������ �� ����������� ��� �������� ����� ����.
    public static MusicManager Instance { get; private set; }

    [Header("Music Clips")]
    [Tooltip("����������� ���� �������� ����.")]
    public AudioClip mainMenuMusic;
    [Tooltip("����������� ���� ��� ������� �������.")]
    public AudioClip levelMusic;

    [Header("Settings")]
    [Tooltip("���������� ��������� ������.")]
    public float normalVolume = 1f;
    [Tooltip("��������� ������ � ������ �����.")]
    public float pausedVolume = 0.2f;
    [Tooltip("����� �� ������� ����������/���������� ���������.")]
    public float fadeDuration = 0.5f;

    private AudioSource audioSource;

    void Awake()
    {
        // ��������� ������� Singleton.
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

        // ������������� �� ������� �������� ����� �����
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // ��� ������� ���� �������� ���� ��� ������� �����.
        SetMusicForScene(SceneManager.GetActiveScene());
    }

    // ��� �������� ����� ����� ���������� ��� �������
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetMusicForScene(scene);
    }

    // �������� ���� � ����������� �� ����� ����������� �����.
    // ��������������, ��� ����� �������� ���� ���������� "MainMenu".
    void SetMusicForScene(Scene scene)
    {
        AudioClip clipToPlay = (scene.name == "MainMenu") ? mainMenuMusic : levelMusic;
        if (audioSource.clip != clipToPlay)
        {
            // ���������� ������� ������������ ������.
            StartCoroutine(SwitchMusic(clipToPlay));
        }
    }

    // ������� ������������ ������ � fade-out ������� ����� � fade-in ������.
    IEnumerator SwitchMusic(AudioClip newClip)
    {
        float startVolume = audioSource.volume;
        float t = 0f;
        // ������� ��������� (fade-out)
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
        // ������� ���������� ��������� (fade-in)
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(0f, normalVolume, t / fadeDuration);
            yield return null;
        }
        audioSource.volume = normalVolume;
    }

    /// <summary>
    /// ��������� ���� ����� �� ������ �������� (��������, ��� ��������/�������� ���� �����),
    /// ����� �������� ��������� ������.
    /// </summary>
    /// <param name="paused">���� true � ���� �� �����, ���� ������������.</param>
    public void SetPaused(bool paused)
    {
        StopAllCoroutines(); // ��������� ����� ������� ��������� ���������, ����� �������� ����������.
        StartCoroutine(FadeVolume(paused ? pausedVolume : normalVolume, fadeDuration));
    }

    // ������� ��������� ��������� �� targetVolume �� ����� duration.
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