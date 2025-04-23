using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // —мещение и размер зоны проверки земли.
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // ≈сли true Ц игнорируем поворот (flip) при проверке стены.
    public bool ignoreFlipForWallChecks = false;

    [Header("Wall Check Collider Settings (Override)")]
    // ≈сли установлено в true, дл€ проверки цеплени€ за стены используютс€ параметры ниже.
    public bool overrideWallCheckCollider = false;
    public Vector2 customWallCheckOffset;
    public Vector2 customWallCheckSize;

    [Header("Model Center Adjustment")]
    // ¬ам нужно задать смещение от точки pivot до визуального центра модели.
    // ѕри повороте эту величину можно зеркально отразить.
    public Vector2 modelCenterOffset;

    // —войства дл€ доступа из других скриптов.
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
        // ѕолучаем мировую позицию проверки земли.
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);
        // ѕроверку стены выполн€ем через метод, использующий две линии.
        IsTouchingWall = CheckFullWallContact();
    }

    // ¬озвращает true, если хот€ бы одна из линий (спереди или сзади) полностью прилегает к стене.
    private bool CheckFullWallContact()
    {
        // ќпредел€ем позицию, скорректированную на смещение центра модели.
        Vector2 pos = (Vector2)transform.position +
                      new Vector2(ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                                  modelCenterOffset.y);

        // ¬ыбираем offset и размер дл€ проверки: либо из компонента, либо из настроек override.
        Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : boxCollider.offset;
        Vector2 size = overrideWallCheckCollider ? customWallCheckSize : boxCollider.size;
        Vector2 halfSize = size * 0.5f;

        bool facingRight = ignoreFlipForWallChecks ? true : (transform.localScale.x >= 0);
        Vector2 frontTop, frontBottom, backTop, backBottom;

        if (facingRight)
        {
            // ≈сли персонаж смотрит вправо, "лини€ спереди" Ц права€ сторона.
            frontTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
        }
        else
        {
            // ≈сли персонаж смотрит влево, "лини€ спереди" Ц лева€ сторона.
            frontTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
        }
        // ѕроверка: если обе контрольные точки линии (верхн€€ и нижн€€) касаютс€ стены, считаем, что лини€ полностью прилегает.
        bool frontFull = Physics2D.OverlapPoint(frontTop, wallLayer) && Physics2D.OverlapPoint(frontBottom, wallLayer);
        bool backFull = Physics2D.OverlapPoint(backTop, wallLayer) && Physics2D.OverlapPoint(backBottom, wallLayer);
        return frontFull || backFull;
    }

    void OnDrawGizmosSelected()
    {
        // ќтрисовка зоны проверки земли.
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        // ≈сли имеетс€ BoxCollider2D, отрисовываем линии дл€ проверки стены.
        BoxCollider2D bc = GetComponent<BoxCollider2D>();
        if (bc != null)
        {
            Vector2 pos = (Vector2)transform.position +
                          new Vector2(ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                                      modelCenterOffset.y);
            Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : bc.offset;
            Vector2 size = overrideWallCheckCollider ? customWallCheckSize : bc.size;
            Vector2 halfSize = size * 0.5f;
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