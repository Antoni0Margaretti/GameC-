using UnityEngine;

public class CollisionController : MonoBehaviour
{
    public enum HitboxState { Normal, Crouching, Sliding }

    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // —мещение и размер зоны проверки земли.
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // ≈сли true Ц игнорировать поворот (flip) при проверке стены.
    public bool ignoreFlipForWallChecks = false;

    [Header("Wall Check Collider Settings (Override)")]
    // ≈сли true, дл€ проверки цеплени€ за стены используютс€ пользовательские параметры.
    public bool overrideWallCheckCollider = false;
    public Vector2 customWallCheckOffset;
    public Vector2 customWallCheckSize;

    [Header("Model Center Adjustment")]
    // —мещение от точки pivot до визуального центра модели. ѕри повороте оно зеркально отражаетс€.
    public Vector2 modelCenterOffset;

    [Header("Wall Contact Buffer Settings")]
    // Ѕуфер времени, в течение которого контакт со стеной считаетс€ действующим, даже если текуща€ проверка не сработала.
    public float wallContactGracePeriod = 0.15f;
    private float lastWallContactTime = -100f;

    [Header("Dynamic Hitbox Settings")]
    // –азмеры и смещени€ хитбокса дл€ различных состо€ний.
    public Vector2 normalHitboxSize;
    public Vector2 normalHitboxOffset;
    public Vector2 crouchingHitboxSize;
    public Vector2 crouchingHitboxOffset;
    public Vector2 slidingHitboxSize;
    public Vector2 slidingHitboxOffset;

    // “екущее состо€ние хитбокса. PlayerController должен обновл€ть его при смене состо€ни€.
    public HitboxState currentHitboxState = HitboxState.Normal;

    // —войства дл€ доступа из других компонентов.
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    private BoxCollider2D boxCollider;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        // ≈сли динамические хитбоксы не заданы через Inspector, используем данные из компонента.
        if (normalHitboxSize == Vector2.zero) normalHitboxSize = boxCollider.size;
        if (normalHitboxOffset == Vector2.zero) normalHitboxOffset = boxCollider.offset;
    }

    void Update()
    {
        UpdateHitbox();
        CheckCollisions();
    }

    /// <summary>
    /// ¬ зависимости от currentHitboxState обновл€ет размеры и смещение BoxCollider2D.
    /// </summary>
    void UpdateHitbox()
    {
        switch (currentHitboxState)
        {
            case HitboxState.Normal:
                boxCollider.size = normalHitboxSize;
                boxCollider.offset = normalHitboxOffset;
                break;
            case HitboxState.Crouching:
                boxCollider.size = crouchingHitboxSize;
                boxCollider.offset = crouchingHitboxOffset;
                break;
            case HitboxState.Sliding:
                boxCollider.size = slidingHitboxSize;
                boxCollider.offset = slidingHitboxOffset;
                break;
        }
    }

    /// <summary>
    /// ¬ыполн€ет проверки: земл€ (OverlapBox) и стена (с м€гким буфером).
    /// </summary>
    private void CheckCollisions()
    {
        // ѕроверка земли.
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        // ѕроверка стены с использованием метода CheckFullWallContact().
        bool fullContact = CheckFullWallContact();
        if (fullContact)
            lastWallContactTime = Time.time;
        // ƒаже если в текущем кадре контакт не найден, если с момента последнего контакта прошло менее wallContactGracePeriod, считаем, что персонаж всЄ еще касаетс€ стены.
        IsTouchingWall = (Time.time - lastWallContactTime) <= wallContactGracePeriod;
    }

    /// <summary>
    /// ѕровер€ет, прилегают ли две вертикальные линии (лицевую и заднюю) хитбокса к стене.
    /// »спользует либо параметры из BoxCollider2D, либо кастомные, если override включен.
    /// </summary>
    /// <returns>True, если хот€ бы одна из линий полностью касаетс€ стены.</returns>
    private bool CheckFullWallContact()
    {
        // –асчет позиции с учетом modelCenterOffset. ≈сли персонаж повернут, ось X зеркально отражаетс€.
        Vector2 pos = (Vector2)transform.position +
                      new Vector2(ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                                  modelCenterOffset.y);
        // »спользуем параметры хитбокса дл€ проверки стен или кастомные, если разрешено.
        Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : boxCollider.offset;
        Vector2 size = overrideWallCheckCollider ? customWallCheckSize : boxCollider.size;
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
        bool frontFull = Physics2D.OverlapPoint(frontTop, wallLayer) && Physics2D.OverlapPoint(frontBottom, wallLayer);
        bool backFull = Physics2D.OverlapPoint(backTop, wallLayer) && Physics2D.OverlapPoint(backBottom, wallLayer);
        return frontFull || backFull;
    }

    /// <summary>
    /// ќтрисовывает Gizmos дл€ отладки зон проверки земли и стены.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // «она проверки земли.
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        // ќтрисовка линий дл€ проверки стены.
        Gizmos.color = Color.red;
        Vector2 pos = (Vector2)transform.position +
            new Vector2(ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                        modelCenterOffset.y);
        Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : boxCollider.offset;
        Vector2 size = overrideWallCheckCollider ? customWallCheckSize : boxCollider.size;
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
        Gizmos.DrawLine(frontTop, frontBottom);
        Gizmos.DrawLine(backTop, backBottom);
    }
}