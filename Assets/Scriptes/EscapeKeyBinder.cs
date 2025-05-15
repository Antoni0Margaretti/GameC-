using UnityEngine;
using UnityEngine.UI;

public class EscapeKeyBinder : MonoBehaviour
{
    [Tooltip("Кнопка, выполняющая действие 'назад', которая будет активироваться по нажатию Esc.")]
    public Button backButton;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Вызываем onClick у кнопки, что эквивалентно её нажатию
            if (backButton != null)
            {
                backButton.onClick.Invoke();
            }
        }
    }
}