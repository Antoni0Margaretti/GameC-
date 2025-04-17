using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // Локальное смещение для проверки земли (например, (0, -0.5f))
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // Локальное смещение для проверки стены (например, (0.4f, 0))
    public Vector2 wallCheckOffset = new Vector2(0.4f, 0);
    public Vector2 wallCheckSize = new Vector2(0.2f, 1.0f);
    // Если true – игнорируем поворот (flip) при проверке стены (фиксированная зона)
    public bool ignoreFlipForWallChecks = false;

    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    void Update()
    {
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        Vector2 wallCheckPos = ignoreFlipForWallChecks ?
            ((Vector2)transform.position + wallCheckOffset) :
            (Vector2)transform.TransformPoint(wallCheckOffset);
        IsTouchingWall = Physics2D.OverlapBox(wallCheckPos, wallCheckSize, 0f, wallLayer);
    }

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
