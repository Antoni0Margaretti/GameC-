using UnityEngine;

public class CollisionController : MonoBehaviour
{
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
    // Если установлено, используются кастомные параметры.
    public bool overrideWallCheckCollider = false;
    public Vector2 customWallCheckOffset;
    public Vector2 customWallCheckSize;

    [Header("Model Center Adjustment")]
    // Смещение от pivot до визуального центра модели (отражается при смене направления).
    public Vector2 modelCenterOffset;

    [Header("Wall Contact Buffer Settings")]
    // Буфер времени для мягкого контакта.
    public float wallContactGracePeriod = 0.15f;
    private float lastWallContactTime = -100f;
    // Храним сторону последнего обнаруженного контакта: 1 – контакт с правой стороны, -1 – с левой.
    private int lastWallContactSide = 0;

    [Header("Dynamic Hitbox Settings")]
    public Vector2 normalHitboxSize;
    public Vector2 normalHitboxOffset;
    public Vector2 crouchingHitboxSize;
    public Vector2 crouchingHitboxOffset;
    public Vector2 slidingHitboxSize;
    public Vector2 slidingHitboxOffset;
    public enum HitboxState { Normal, Crouching, Sliding }
    // Текущее состояние хитбокса (PlayerController задаёт это свойство).
    public HitboxState currentHitboxState = HitboxState.Normal;

    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    private BoxCollider2D boxCollider;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        // Если не заданы динамические параметры, подставляем размеры поля Collider'а.
        if (normalHitboxSize == Vector2.zero) normalHitboxSize = boxCollider.size;
        if (normalHitboxOffset == Vector2.zero) normalHitboxOffset = boxCollider.offset;
    }

    void Update()
    {
        UpdateHitbox();
        CheckCollisions();
    }

    /// <summary>
    /// Обновляет размеры и смещение BoxCollider2D согласно текущему состоянию хитбокса.
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
    /// Выполняет проверки земли и стены.
    /// </summary>
    private void CheckCollisions()
    {
        // Земля.
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        // Стена.
        bool fullContact = CheckFullWallContact();
        if (fullContact)
        {
            lastWallContactTime = Time.time;
        }
        // Получаем ожидаемую сторону для контакта в зависимости от текущего направления.
        int expectedSide = ignoreFlipForWallChecks ? 1 : (transform.localScale.x >= 0 ? 1 : -1);

        // Состояние стены активно, если временной буфер ещё не истёк и обнаруженная сторона совпадает с ожидаемой.
        IsTouchingWall = ((Time.time - lastWallContactTime) <= wallContactGracePeriod) && (lastWallContactSide == expectedSide);
    }

    /// <summary>
    /// Проверяет, прилегают ли "линия спереди" и "линия сзади" хитбокса к стене.
    /// Сохраняет сторону контакта в lastWallContactSide:
    ///   если контакт обнаружен по лицевой стороне – равна (facing ? 1 : -1),
    ///   если по задней – равна (facing ? -1 : 1).
    /// </summary>
    /// <returns>True, если хотя бы одна линия полностью касается стены.</returns>
    private bool CheckFullWallContact()
    {
        // Вычисляем позицию с учетом modelCenterOffset.
        Vector2 pos = (Vector2)transform.position +
                      new Vector2(ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                                  modelCenterOffset.y);
        // Используем либо параметры Collider'а, либо кастомные.
        Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : boxCollider.offset;
        Vector2 size = overrideWallCheckCollider ? customWallCheckSize : boxCollider.size;
        Vector2 halfSize = size * 0.5f;

        bool facingRight = ignoreFlipForWallChecks ? true : (transform.localScale.x >= 0);
        Vector2 frontTop, frontBottom, backTop, backBottom;
        if (facingRight)
        {
            // Если персонаж смотрит вправо, "линия спереди" – правая сторона.
            frontTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
        }
        else
        {
            // Если персонаж смотрит влево, "линия спереди" – левая сторона.
            frontTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
        }

        bool frontFull = Physics2D.OverlapPoint(frontTop, wallLayer) && Physics2D.OverlapPoint(frontBottom, wallLayer);
        bool backFull = Physics2D.OverlapPoint(backTop, wallLayer) && Physics2D.OverlapPoint(backBottom, wallLayer);

        // Сохраняем detectedSide: если контакт обнаружен на лицевой стороне – равна 1 (при facingRight) или -1 (при facingLeft),
        // если на задней стороне – обратное значение.
        if (frontFull)
        {
            lastWallContactSide = facingRight ? 1 : -1;
            return true;
        }
        else if (backFull)
        {
            lastWallContactSide = facingRight ? -1 : 1;
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Метод сброса состояния буфера контакта (может быть вызван извне, например, при смене направления).
    /// </summary>
    public void ResetWallContactBuffer()
    {
        lastWallContactTime = -100f;
        lastWallContactSide = 0;
    }

    void OnDrawGizmosSelected()
    {
        // Отрисовка зоны проверки земли.
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        // Отрисовка "линий" для проверки стены.
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

    public bool WasTouchingWallRecently()
    {
        return (Time.time - lastWallContactTime) <= wallContactGracePeriod;
    }

    public int GetLastWallContactSide()
    {
        return lastWallContactSide;
    }

}