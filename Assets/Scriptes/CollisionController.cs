using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Настройки проверки земли")]
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.25f;

    [Header("Настройки проверки стены")]
    public LayerMask wallLayer;
    public float wallCheckRadius = 0.2f;

    // Свойства для доступа из другого скрипта
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    void Update()
    {
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        Vector2 pos = transform.position;
        IsGrounded = Physics2D.OverlapCircle(pos, groundCheckRadius, groundLayer);
        IsTouchingWall = Physics2D.OverlapCircle(pos, wallCheckRadius, wallLayer);
    }

    // Для визуальной отладки (чат: убедитесь, что Gizmos включены в сцене)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, groundCheckRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, wallCheckRadius);
    }
}