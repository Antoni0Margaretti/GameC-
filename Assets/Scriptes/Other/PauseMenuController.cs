using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    // Ссылка на панель меню паузы – установите через Inspector (перетащите PauseMenuPanel)
    public GameObject pauseMenuPanel;
    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyBindings.Pause))
        {
            if (!isPaused)
                PauseGame();
            else
                ResumeGame();
        }
    }

    // Метод для постановки игры на паузу
    public void PauseGame()
    {
        pauseMenuPanel.SetActive(true); // Показываем панель меню паузы
        Time.timeScale = 0f;            // Останавливаем игровой процесс, но не рендеринг
        isPaused = true;
    }

    // Метод для возобновления игры
    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false); // Скрываем меню паузы
        Time.timeScale = 1f;             // Возвращаем нормальное течение времени
        isPaused = false;
    }

    // Метод для выхода в главное меню (если требуется)
    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
