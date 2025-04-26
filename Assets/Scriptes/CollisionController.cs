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
    // Если true, используются кастомные параметры для проверки стены.
    public bool overrideWallCheckCollider = false;
    public Vector2 customWallCheckOffset;
    public Vector2 customWallCheckSize;

    [Header("Model Center Adjustment")]
    // Смещение от точки pivot до визуального центра модели (при повороте зеркально отражается).
    public Vector2 modelCenterOffset;

    [Header("Wall Contact Buffer Settings")]
    // Время (сек), в течение которого контакт со стеной считается действующим.
    public float wallContactGracePeriod = 0.15f;
    private float lastWallContactTime = -100f;
    // Хранит сторону последнего контакта: 1 – если стена справа, -1 – если слева.
    private int lastWallContactSide = 0;

    // Старую логику динамических хитбоксов мы убираем.
    // Поля, связанные с normalHitboxSize, crouchingHitboxSize, slidingHitboxSize, 
    // а также метод UpdateHitbox() – больше не нужны.

    // Текущее состояние хитбокса (оставляем, если оно используется в логике, например,
    // для любых анимационных состояний, но обновление Hitbox будет производить другой скрипт).
    public HitboxState currentHitboxState = HitboxState.Normal;

    // Свойства для доступа: которые нужны для проверки столкновений.
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    // Ссылка для расчётов используется BoxCollider2D, настроенный в редакторе.
    // Если вы используете динамический хитбокс через DynamicSpriteCollider, то у вас может быть
    // PolygonCollider2D вместо BoxCollider2D, но для проверки (например, OverlapBox) можно оставить
    // BoxCollider2D или задать отдельные параметры.
    private BoxCollider2D boxCollider;

    void Start()
    {
        // Получаем BoxCollider2D только для расчётов методами проверки столкновений.
        boxCollider = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        // Обновляем чаще только проверки столкновений.
        CheckCollisions();
    }

    /// <summary>
    /// Проверяет столкновения с землёй и стеной.
    /// </summary>
    void CheckCollisions()
    {
        // Проверка земли: рассчитываем мировую позицию точки проверки, используя groundCheckOffset.
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundPos, groundCheckSize, 0f, groundLayer);

        // Проверка стены: используем метод CheckFullWallContact().
        bool fullContact = CheckFullWallContact();

        // Если в этом кадре контакт обнаружен, фиксируем время контакта.
        if (fullContact && !IsTouchingWall)
        {
            lastWallContactTime = Time.time;
        }

        // Если контакт обнаружён или недавно (в течение wallContactGracePeriod) был – считаем, что стена касается.
        IsTouchingWall = fullContact || ((Time.time - lastWallContactTime) <= wallContactGracePeriod);
    }

    /// <summary>
    /// Выполняет проверку контакта со стеной, вычисляя контрольные точки по бокам хитбокса.
    /// </summary>
    bool CheckFullWallContact()
    {
        // Вычисляем мировую позицию с учётом modelCenterOffset.
        Vector2 pos = (Vector2)transform.position +
                      new Vector2(
                          ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                          modelCenterOffset.y);

        // Определяем offset и размер для проверки – либо кастомные, либо из BoxCollider2D.
        Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : boxCollider.offset;
        Vector2 size = overrideWallCheckCollider ? customWallCheckSize : boxCollider.size;
        Vector2 halfSize = size * 0.5f;

        bool facingRight = ignoreFlipForWallChecks ? true : (transform.localScale.x >= 0);

        // Вычисляем контрольные точки для лицевой и задней сторон хитбокса.
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
    /// Возвращает сторону последнего контакта со стеной: 1 – если стена справа, -1 – если слева.
    /// </summary>
    public int GetLastWallContactSide()
    {
        return lastWallContactSide;
    }

    /// <summary>
    /// Сбрасывает буфер контакта со стеной.
    /// </summary>
    public void ResetWallContactBuffer()
    {
        lastWallContactTime = -100f;
        lastWallContactSide = 0;
    }

    void OnDrawGizmosSelected()
    {
        // Отрисовываем зону проверки земли.
        Gizmos.color = Color.green;
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundPos, groundCheckSize);

        // Отрисовываем линию проверки стены.
        Gizmos.color = Color.red;
        if (boxCollider != null)
        {
            Vector2 pos = (Vector2)transform.position +
                          new Vector2(
                              ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
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

    /// <summary>
    /// Проверяет, есть ли пол под заданным объектом (например, при проверке выступа).
    /// </summary>
    public bool IsLedgeClear(Transform ledgeOrigin, float rayLength)
    {
        RaycastHit2D hit = Physics2D.Raycast(ledgeOrigin.position, Vector2.down, rayLength, groundLayer);
        Debug.DrawRay(ledgeOrigin.position, Vector2.down * rayLength, Color.yellow);
        return (hit.collider == null);
    }
}
