using System.Collections.Generic;
using UnityEngine;

// Обеспечиваем наличие необходимых компонентов на объекте.
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DynamicSpriteCollider : MonoBehaviour
{
    // Ссылка на компонент PolygonCollider2D, который мы будем обновлять.
    private PolygonCollider2D polyCollider;
    // Ссылка на SpriteRenderer, чтобы получать текущий спрайт объекта.
    private SpriteRenderer spriteRenderer;
    // Сохраняем последний использованный спрайт, чтобы не обновлять хитбокс лишний раз.
    private Sprite lastSprite;

    void Awake()
    {
        // Инициализируем ссылки на компоненты.
        polyCollider = GetComponent<PolygonCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // При старте обновляем хитбокс в соответствии с первоначальным спрайтом.
        UpdateCollider();
    }

    void Update()
    {
        // Если спрайт изменился (например, в анимации), обновляем форму хитбокса.
        if (spriteRenderer.sprite != lastSprite)
        {
            UpdateCollider();
        }
    }

    /// <summary>
    /// Обновляет форму PolygonCollider2D на основе физической формы текущего спрайта.
    /// Форма берётся из настроек спрайта, заданных в Sprite Editor (Custom Physics Shape).
    /// </summary>
    public void UpdateCollider()
    {
        // Если спрайт не назначен, ничего не делаем.
        if (spriteRenderer.sprite == null)
            return;

        // Обновляем последний использованный спрайт.
        lastSprite = spriteRenderer.sprite;

        // Получаем количество контуров (форм) физической формы, заданной для спрайта.
        int shapeCount = spriteRenderer.sprite.GetPhysicsShapeCount();
        // Устанавливаем количество путей в PolygonCollider2D равным числу контуров.
        polyCollider.pathCount = shapeCount;

        // Временный список для хранения точек одного контура.
        List<Vector2> shapePoints = new List<Vector2>();

        // Для каждого контура получаем набор вершин и назначаем его в PolygonCollider2D
        for (int i = 0; i < shapeCount; i++)
        {
            // Очистка списка на случай, если он уже содержит данные.
            shapePoints.Clear();
            // Заполняем список точками контура с индексом i.
            spriteRenderer.sprite.GetPhysicsShape(i, shapePoints);
            // Назначаем полученный массив точек в качестве пути коллайдера.
            polyCollider.SetPath(i, shapePoints.ToArray());
        }
    }
}