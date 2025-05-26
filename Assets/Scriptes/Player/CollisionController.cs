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

    // Текущее состояние хитбокса (оставляем, если оно используется в логике, например,
    // для любых анимационных состояний, но обновление Hitbox будет производить другой скрипт).
    public HitboxState currentHitboxState = HitboxState.Normal;

    // Свойства для доступа: которые нужны для проверки столкновений.
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    // Ссылки для расчётов используются BoxCollider2D и PolygonCollider2D.
    private BoxCollider2D boxCollider;
    private PolygonCollider2D polyCollider;

    void Start()
    {
        // Получаем BoxCollider2D и PolygonCollider2D только для расчётов методами проверки столкновений.
        boxCollider = GetComponent<BoxCollider2D>();
        polyCollider = GetComponent<PolygonCollider2D>();
    }

    void Update()
    {
        // Обновляем чаще только проверки столкновений.
        CheckCollisions();
    }

    /// Проверяет столкновения с землёй и стеной.

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

    /// Выполняет проверку контакта со стеной, вычисляя контрольные точки по бокам хитбокса.

    bool CheckFullWallContact()
    {
        // Если есть BoxCollider2D — используем старую логику
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

            bool frontFull = Physics2D.OverlapBox(frontTop, new Vector2(0.05f, boxCollider.size.y), 0f, wallLayer);
            bool backFull = Physics2D.OverlapBox(backTop, new Vector2(0.05f, boxCollider.size.y), 0f, wallLayer);

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
        // Если BoxCollider2D нет, но есть PolygonCollider2D — используем его bounds
        else if (polyCollider != null)
        {
            Bounds bounds = polyCollider.bounds;
            float sideOffset = bounds.extents.x * 0.95f;
            float top = bounds.max.y - 0.01f;
            float bottom = bounds.min.y + 0.01f;
            Vector2 pos = (Vector2)transform.position;

            // Проверяем точки по бокам полигона
            Vector2 rightTop = new Vector2(bounds.center.x + sideOffset, top);
            Vector2 rightBottom = new Vector2(bounds.center.x + sideOffset, bottom);
            Vector2 leftTop = new Vector2(bounds.center.x - sideOffset, top);
            Vector2 leftBottom = new Vector2(bounds.center.x - sideOffset, bottom);

            bool rightFull = Physics2D.OverlapPoint(rightTop, wallLayer) && Physics2D.OverlapPoint(rightBottom, wallLayer);
            bool leftFull = Physics2D.OverlapPoint(leftTop, wallLayer) && Physics2D.OverlapPoint(leftBottom, wallLayer);

            if (rightFull)
            {
                lastWallContactSide = 1;
                return true;
            }
            else if (leftFull)
            {
                lastWallContactSide = -1;
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    /// Возвращает сторону последнего контакта со стеной: 1 – если стена справа, -1 – если слева.

    public int GetLastWallContactSide()
    {
        return lastWallContactSide;
    }


    /// Сбрасывает буфер контакта со стеной.

    public void ResetWallContactBuffer()
    {
        lastWallContactTime = -100f;
        lastWallContactSide = 0;
    }

    /// Отключает контакт со стеной.
    public void DetachFromWall()
    {
        IsTouchingWall = false;
        ResetWallContactBuffer();
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

    /// Проверяет, есть ли пол под заданным объектом (например, при проверке выступа).
    public bool IsLedgeClear(Transform ledgeOrigin, float rayLength)
    {
        RaycastHit2D hit = Physics2D.Raycast(ledgeOrigin.position, Vector2.down, rayLength, groundLayer);
        Debug.DrawRay(ledgeOrigin.position, Vector2.down * rayLength, Color.yellow);
        return (hit.collider == null);
    }
}
