using UnityEngine;

public class MenuParallax : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxElement
    {
        // Объект, который нужно смещать (камера, логотип, элемент UI)
        public Transform target;
        // Сила смещения для данного элемента
        public float strength = 0.1f;
        // Запоминаем исходную позицию, чтобы смещение было относительно неё
        [HideInInspector] public Vector3 initialPosition;
    }

    // Массив объектов, на которые будет влиять эффект параллакса
    public ParallaxElement[] elements;

    // Скорость сглаживания движения
    public float smoothSpeed = 5f;

    void Start()
    {
        // Сохраняем начальные позиции для всех элементов
        foreach (var elem in elements)
        {
            if (elem.target != null)
            {
                elem.initialPosition = elem.target.position;
            }
        }
    }

    void Update()
    {
        // Получаем позицию мыши
        Vector2 mousePos = Input.mousePosition;
        // Определяем центр экрана
        float halfScreenWidth = Screen.width / 2f;
        float halfScreenHeight = Screen.height / 2f;
        // Нормализуем смещение мыши относительно центра (значения от -1 до 1)
        float normalizedX = (mousePos.x - halfScreenWidth) / halfScreenWidth;
        float normalizedY = (mousePos.y - halfScreenHeight) / halfScreenHeight;

        // Применяем смещение к каждому элементу
        foreach (var elem in elements)
        {
            if (elem.target != null)
            {
                // Вычисляем желаемое смещение для элемента
                Vector3 offset = new Vector3(normalizedX * elem.strength, normalizedY * elem.strength, 0f);
                Vector3 desiredPosition = elem.initialPosition + offset;
                // Плавно перемещаем элемент к желаемой позиции
                elem.target.position = Vector3.Lerp(elem.target.position, desiredPosition, Time.deltaTime * smoothSpeed);
            }
        }
    }
}
