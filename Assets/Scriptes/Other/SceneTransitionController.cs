using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionController : MonoBehaviour
{
    [Header("Настройки перехода")]
    [Tooltip("Материал с шейдером эффекта перехода")]
    public Material transitionMaterial;  // Материал с вашим шейдером (DepixelTransition)
    [Tooltip("Длительность перехода (в секундах)")]
    public float transitionDuration = 1.5f; // Длительность перехода

    private static SceneTransitionController instance;

    private void Awake()
    {
        // Если требуется сохранять объект перехода между сценами.
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
        // При загрузке новой сцены (например, Level1Scene) выполняется переход "входа" — депикселизация.
        StartCoroutine(TransitionIn());
    }

    /// <summary>
    /// Плавное очищение экрана: значение _Pixelation уменьшается (от 100 до 1),
    /// а _Fade уменьшает затемнение (от 1 до 0), показывая содержимое сцены.
    /// Обычно используется при появлении новой сцены (Transition In).
    /// </summary>
    public IEnumerator TransitionIn()
    {
        float timer = 0f;

        // Устанавливаем начальные значения: экран полностью пикселизирован и затемнен.
        transitionMaterial.SetFloat("_Pixelation", 100f);
        transitionMaterial.SetFloat("_Fade", 1f);

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;

            // Депикселизация: уменьшаем значение пикселизации от 100 до 1
            // и затемнение от 1 до 0.
            float pixelation = Mathf.Lerp(100f, 1f, t);
            float fade = Mathf.Lerp(1f, 0f, t);

            transitionMaterial.SetFloat("_Pixelation", pixelation);
            transitionMaterial.SetFloat("_Fade", fade);

            yield return null;
        }

        // Окончательно делаем изображение чистым.
        transitionMaterial.SetFloat("_Pixelation", 1f);
        transitionMaterial.SetFloat("_Fade", 0f);
    }

    /// <summary>
    /// Плавный переход при смене сцены (Transition Out):
    /// начинает с нормального состояния (_Pixelation = 1, _Fade = 0) и постепенно
    /// увеличивает пикселизацию (до 100) и затемняет изображение (до 1). После завершения
    /// анимации загружает указанную сцену.
    /// </summary>
    /// <param name="sceneName">Имя следующей сцены для загрузки</param>
    public IEnumerator TransitionOut(string sceneName)
    {
        float timer = 0f;

        // Изначально предполагаем, что экран чистый.
        transitionMaterial.SetFloat("_Pixelation", 1f);
        transitionMaterial.SetFloat("_Fade", 0f);

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;

            // Пикселизация: увеличиваем _Pixelation от 1 до 100
            // Затемнение: увеличиваем _Fade от 0 до 1
            float pixelation = Mathf.Lerp(1f, 100f, t);
            float fade = Mathf.Lerp(0f, 1f, t);

            transitionMaterial.SetFloat("_Pixelation", pixelation);
            transitionMaterial.SetFloat("_Fade", fade);

            yield return null;
        }

        // Убеждаемся, что значения достигли нужных концов.
        transitionMaterial.SetFloat("_Pixelation", 100f);
        transitionMaterial.SetFloat("_Fade", 1f);

        // Загружаем новую сцену.
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Метод, который можно вызвать из OnClick() кнопки, например, кнопки "Играть" в MainMenu.
    /// </summary>
    /// <param name="sceneName">Имя сцены для загрузки (например, "Level1Scene")</param>
    public void StartTransitionOut(string sceneName)
    {
        StartCoroutine(TransitionOut(sceneName));
    }
}