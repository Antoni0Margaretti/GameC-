using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionController : MonoBehaviour
{
    [Header("��������� ��������")]
    [Tooltip("�������� � �������� ������� ��������")]
    public Material transitionMaterial;  // �������� � ����� �������� (DepixelTransition)
    [Tooltip("������������ �������� (� ��������)")]
    public float transitionDuration = 1.5f; // ������������ ��������

    private static SceneTransitionController instance;

    private void Awake()
    {
        // ���� ��������� ��������� ������ �������� ����� �������.
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // ��� �������� ����� ����� (��������, Level1Scene) ����������� ������� "�����" � ��������������.
        StartCoroutine(TransitionIn());
    }

    /// <summary>
    /// ������� �������� ������: �������� _Pixelation ����������� (�� 100 �� 1),
    /// � _Fade ��������� ���������� (�� 1 �� 0), ��������� ���������� �����.
    /// ������ ������������ ��� ��������� ����� ����� (Transition In).
    /// </summary>
    public IEnumerator TransitionIn()
    {
        float timer = 0f;

        // ������������� ��������� ��������: ����� ��������� �������������� � ��������.
        transitionMaterial.SetFloat("_Pixelation", 100f);
        transitionMaterial.SetFloat("_Fade", 1f);

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;

            // ��������������: ��������� �������� ������������ �� 100 �� 1
            // � ���������� �� 1 �� 0.
            float pixelation = Mathf.Lerp(100f, 1f, t);
            float fade = Mathf.Lerp(1f, 0f, t);

            transitionMaterial.SetFloat("_Pixelation", pixelation);
            transitionMaterial.SetFloat("_Fade", fade);

            yield return null;
        }

        // ������������ ������ ����������� ������.
        transitionMaterial.SetFloat("_Pixelation", 1f);
        transitionMaterial.SetFloat("_Fade", 0f);
    }

    /// <summary>
    /// ������� ������� ��� ����� ����� (Transition Out):
    /// �������� � ����������� ��������� (_Pixelation = 1, _Fade = 0) � ����������
    /// ����������� ������������ (�� 100) � ��������� ����������� (�� 1). ����� ����������
    /// �������� ��������� ��������� �����.
    /// </summary>
    /// <param name="sceneName">��� ��������� ����� ��� ��������</param>
    public IEnumerator TransitionOut(string sceneName)
    {
        float timer = 0f;

        // ���������� ������������, ��� ����� ������.
        transitionMaterial.SetFloat("_Pixelation", 1f);
        transitionMaterial.SetFloat("_Fade", 0f);

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;

            // ������������: ����������� _Pixelation �� 1 �� 100
            // ����������: ����������� _Fade �� 0 �� 1
            float pixelation = Mathf.Lerp(1f, 100f, t);
            float fade = Mathf.Lerp(0f, 1f, t);

            transitionMaterial.SetFloat("_Pixelation", pixelation);
            transitionMaterial.SetFloat("_Fade", fade);

            yield return null;
        }

        // ����������, ��� �������� �������� ������ ������.
        transitionMaterial.SetFloat("_Pixelation", 100f);
        transitionMaterial.SetFloat("_Fade", 1f);

        // ��������� ����� �����.
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// �����, ������� ����� ������� �� OnClick() ������, ��������, ������ "������" � MainMenu.
    /// </summary>
    /// <param name="sceneName">��� ����� ��� �������� (��������, "Level1Scene")</param>
    public void StartTransitionOut(string sceneName)
    {
        StartCoroutine(TransitionOut(sceneName));
    }
}