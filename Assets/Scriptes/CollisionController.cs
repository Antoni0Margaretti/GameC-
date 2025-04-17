using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Настройки проверки земли")]
    public LayerMask groundLayer;
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Настройки проверки стены")]
    public LayerMask wallLayer;
    public Vector2 wallCheckOffset = new Vector2(0.4f, 0);
    public Vector2 wallCheckSize = new Vector2(0.2f, 1.0f);

    // Свойства для доступа из другого скрипта
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    void Update()
    {
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        // Проверка земли: смещаем зону проверки от центра персонажа
        Vector2 groundCheckPos = (Vector2)transform.position + groundCheckOffset;
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0, groundLayer);

        // Проверка стены: смещаем зону проверки по горизонтали
        Vector2 wallCheckPos = (Vector2)transform.position + wallCheckOffset;
        IsTouchingWall = Physics2D.OverlapBox(wallCheckPos, wallCheckSize, 0, wallLayer);
    }

    // Для визуальной отладки — отображение зон проверки
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        Gizmos.color = Color.red;
        Vector2 wallCheckPos = (Vector2)transform.position + wallCheckOffset;
        Gizmos.DrawWireCube(wallCheckPos, wallCheckSize);
    }
}
