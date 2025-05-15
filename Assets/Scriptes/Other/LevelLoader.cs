using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    // ����� ��� �������� ����� ������ �� �����
    public void LoadLevel(string levelSceneName)
    {
        SceneManager.LoadScene(levelSceneName);
    }
}