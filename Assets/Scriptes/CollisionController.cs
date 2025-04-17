using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Настройки проверки земли")]
    public LayerMask groundLayer;
    // Локальное смещение для проверки земли (например: (0, -0.5f))
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Настройки проверки стены")]
    public LayerMask wallLayer;
    // Локальное смещение для проверки стены (например: (0.4f, 0))
    public Vector2 wallCheckOffset = new Vector2(0.4f, 0);
    public Vector2 wallCheckSize = new Vector2(0.2f, 1.0f);
    // Если true – смещение для проверки стены применяется без учета поворота (флипа)
    public bool ignoreFlipForWallChecks = false;

    // Свойства для доступа из других скриптов
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    void Update()
    {
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        // Преобразуем локальное смещение для земли в мировые координаты
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        // Для стены используем либо преобразование смещения, либо смещение от центра объекта (если игнорируем флип)
        Vector2 wallCheckPos = ignoreFlipForWallChecks ?
            ((Vector2)transform.position + wallCheckOffset) :
            (Vector2)transform.TransformPoint(wallCheckOffset);
        IsTouchingWall = Physics2D.OverlapBox(wallCheckPos, wallCheckSize, 0f, wallLayer);
    }

    // Визуальная отладка – рисуем области проверки
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        Gizmos.color = Color.red;
        Vector2 wallCheckPos = ignoreFlipForWallChecks ?
            ((Vector2)transform.position + wallCheckOffset) :
            (Vector2)transform.TransformPoint(wallCheckOffset);
        Gizmos.DrawWireCube(wallCheckPos, wallCheckSize);
    }
}
