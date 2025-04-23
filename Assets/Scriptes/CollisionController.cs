using UnityEngine;

public class CollisionController : MonoBehaviour
{
    public enum HitboxState { Normal, Crouching, Sliding }

    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // Смещение и размер зоны проверки земли.
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // Если true – игнорировать поворот (flip) при проверке стены.
    public bool ignoreFlipForWallChecks = false;

    [Header("Wall Check Collider Settings (Override)")]
    // Если true, для проверки цепления за стены используются пользовательские параметры.
    public bool overrideWallCheckCollider = false;
    public Vector2 customWallCheckOffset;
    public Vector2 customWallCheckSize;

    [Header("Model Center Adjustment")]
    // Смещение от точки pivot до визуального центра модели. При повороте оно зеркально отражается.
    public Vector2 modelCenterOffset;

    [Header("Wall Contact Buffer Settings")]
    // Буфер времени, в течение которого контакт со стеной считается действующим, даже если текущая проверка не сработала.
    public float wallContactGracePeriod = 0.15f;
    private float lastWallContactTime = -100f;

    [Header("Dynamic Hitbox Settings")]
    // Размеры и смещения хитбокса для различных состояний.
    public Vector2 normalHitboxSize;
    public Vector2 normalHitboxOffset;
    public Vector2 crouchingHitboxSize;
    public Vector2 crouchingHitboxOffset;
    public Vector2 slidingHitboxSize;
    public Vector2 slidingHitboxOffset;

    // Текущее состояние хитбокса. PlayerController должен обновлять его при смене состояния.
    public HitboxState currentHitboxState = HitboxState.Normal;

    // Свойства для доступа из других компонентов.
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    private BoxCollider2D boxCollider;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        // Если динамические хитбоксы не заданы через Inspector, используем данные из компонента.
        if (normalHitboxSize == Vector2.zero) normalHitboxSize = boxCollider.size;
        if (normalHitboxOffset == Vector2.zero) normalHitboxOffset = boxCollider.offset;
    }

    void Update()
    {
        UpdateHitbox();
        CheckCollisions();
    }

    /// <summary>
    /// В зависимости от currentHitboxState обновляет размеры и смещение BoxCollider2D.
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
    /// Выполняет проверки: земля (OverlapBox) и стена (с мягким буфером).
    /// </summary>
    private void CheckCollisions()
    {
        // Проверка земли.
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        // Проверка стены с использованием метода CheckFullWallContact().
        bool fullContact = CheckFullWallContact();
        if (fullContact)
            lastWallContactTime = Time.time;
        // Даже если в текущем кадре контакт не найден, если с момента последнего контакта прошло менее wallContactGracePeriod, считаем, что персонаж всё еще касается стены.
        IsTouchingWall = (Time.time - lastWallContactTime) <= wallContactGracePeriod;
    }

    /// <summary>
    /// Проверяет, прилегают ли две вертикальные линии (лицевую и заднюю) хитбокса к стене.
    /// Использует либо параметры из BoxCollider2D, либо кастомные, если override включен.
    /// </summary>
    /// <returns>True, если хотя бы одна из линий полностью касается стены.</returns>
    private bool CheckFullWallContact()
    {
        // Расчет позиции с учетом modelCenterOffset. Если персонаж повернут, ось X зеркально отражается.
        Vector2 pos = (Vector2)transform.position +
                      new Vector2(ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                                  modelCenterOffset.y);
        // Используем параметры хитбокса для проверки стен или кастомные, если разрешено.
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
    /// Отрисовывает Gizmos для отладки зон проверки земли и стены.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // Зона проверки земли.
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        // Отрисовка линий для проверки стены.
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

    public void ResetWallContactBuffer()
    {
        // Устанавливаем время последнего контакта в очень старое значение,
        // чтобы на следующий кадр IsTouchingWall не считалось активным.
        lastWallContactTime = -100f;
    }
}