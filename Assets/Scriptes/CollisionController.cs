using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // Ћокальное смещение дл€ проверки земли (например, (0, -0.5f))
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // Ћокальное смещение дл€ проверки стены (например, (0.4f, 0))
    public Vector2 wallCheckOffset = new Vector2(0.4f, 0);
    public Vector2 wallCheckSize = new Vector2(0.2f, 1.0f);
    // ≈сли true Ц игнорируем флип при проверке стены (фиксированное смещение)
    public bool ignoreFlipForWallChecks = false;

    // —войства дл€ доступа из других скриптов
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    void Update()
    {
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        // ѕреобразуем локальное смещение дл€ земли
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        // ƒл€ стены: если ignoreFlipForWallChecks==true Ц используем позицию объекта + смещение,
        // иначе Ц преобразуем смещение через TransformPoint
        Vector2 wallCheckPos = ignoreFlipForWallChecks ?
            ((Vector2)transform.position + wallCheckOffset) :
            (Vector2)transform.TransformPoint(wallCheckOffset);
        IsTouchingWall = Physics2D.OverlapBox(wallCheckPos, wallCheckSize, 0f, wallLayer);
    }

    // ƒл€ визуальной отладки: рисуем области проверки
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