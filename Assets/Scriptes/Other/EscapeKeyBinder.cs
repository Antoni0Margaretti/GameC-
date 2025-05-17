using UnityEngine;
using UnityEngine.UI;

public class EscapeKeyBinder : MonoBehaviour
{
    [Tooltip(" нопка, выполн€юща€ действие 'назад', котора€ будет активироватьс€ по нажатию Esc.")]
    public Button backButton;

    void Update()
    {
        if (Input.GetKeyDown(KeyBindings.Pause))
        {
            if (backButton != null)
            {
                backButton.onClick.Invoke();
            }
        }
    }
}