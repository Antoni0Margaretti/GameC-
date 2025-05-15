using UnityEngine;
using UnityEngine.UI;

public class EscapeKeyBinder : MonoBehaviour
{
    [Tooltip("������, ����������� �������� '�����', ������� ����� �������������� �� ������� Esc.")]
    public Button backButton;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // �������� onClick � ������, ��� ������������ � �������
            if (backButton != null)
            {
                backButton.onClick.Invoke();
            }
        }
    }
}