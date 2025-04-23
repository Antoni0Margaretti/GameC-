using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // Локальное смещение и размер для проверки земли.
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // Если true – игнорируем поворот (flip) при проверке стены.
    public bool ignoreFlipForWallChecks = false;

    // Свойства для доступа из других скриптов.
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    private BoxCollider2D boxCollider;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        // Проверка земли – остаётся без изменений.
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        // Проверка стены. Вместо OverlapBox используется проверка двух линий.
        IsTouchingWall = CheckFullWallContact();
    }

    // Возвращает true, если хотя бы одна из линий (спереди или сзади) полностью прилегает к стене.
    private bool CheckFullWallContact()
    {
        // Получаем мировую позицию центра, смещение и половину размера BoxCollider2D.
        Vector2 pos = (Vector2)transform.position;
        Vector2 offset = boxCollider.offset;
        Vector2 halfSize = boxCollider.size * 0.5f;

        // Определяем направление персонажа.
        bool facingRight = ignoreFlipForWallChecks ? true : (transform.localScale.x >= 0);

        Vector2 frontTop, frontBottom, backTop, backBottom;

        if (facingRight)
        {
            // Если персонаж смотрит вправо, то «линия спереди» – правая сторона, а «линия сзади» – левая.
            frontTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
        }
        else
        {
            // Если персонаж смотрит влево, то «линия спереди» – левая сторона, а «линия сзади» – правая.
            frontTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
        }

        // «Полностью прилегает» означает, что обе контрольные точки линии (верхняя и нижняя) обнаруживают столкновение с объектом из слоя стены.
        bool frontFull = (Physics2D.OverlapPoint(frontTop, wallLayer) && Physics2D.OverlapPoint(frontBottom, wallLayer));
        bool backFull = (Physics2D.OverlapPoint(backTop, wallLayer) && Physics2D.OverlapPoint(backBottom, wallLayer));

        return frontFull || backFull;
    }

    // Для визуальной отладки – рисуем зоны проверки.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        // Если имеется BoxCollider2D, рисуем линии для проверки стены.
        BoxCollider2D bc = GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            Vector2 pos = (Vector2)transform.position;
            Vector2 offset = bc.offset;
            Vector2 halfSize = bc.size * 0.5f;
            bool facingRight = ignoreFlipForWallChecks ? true : (transform.localScale.x >= 0);

            Vector2 frontTop, frontBottom, backTop, backBottom;
            if (facingRight)
            {
                frontTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
                frontBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
                backTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
                backBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
            }
            else
            {
                frontTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
                frontBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
                backTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
                backBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
            }
            Gizmos.color = Color.red;
            Gizmos.DrawLine(frontTop, frontBottom);
            Gizmos.DrawLine(backTop, backBottom);
        }
    }
}
