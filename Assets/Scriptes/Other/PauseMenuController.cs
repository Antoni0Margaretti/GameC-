using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    // ������ �� ������ ���� ����� � ���������� ����� Inspector (���������� PauseMenuPanel)
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

    // ����� ��� ���������� ���� �� �����
    public void PauseGame()
    {
        pauseMenuPanel.SetActive(true); // ���������� ������ ���� �����
        Time.timeScale = 0f;            // ������������� ������� �������, �� �� ���������
        isPaused = true;
    }

    // ����� ��� ������������� ����
    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false); // �������� ���� �����
        Time.timeScale = 1f;             // ���������� ���������� ������� �������
        isPaused = false;
    }

    // ����� ��� ������ � ������� ���� (���� ���������)
    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
