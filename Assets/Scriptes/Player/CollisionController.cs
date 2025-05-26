using UnityEngine;

public class CollisionController : MonoBehaviour
{
    public enum HitboxState { Normal, Crouching, Sliding }

    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // �������� � ������ ���� �������� �����.
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // ���� true � ������������ ������� (flip) ��� �������� �����.
    public bool ignoreFlipForWallChecks = false;

    [Header("Wall Check Collider Settings (Override)")]
    // ���� true, ������������ ��������� ��������� ��� �������� �����.
    public bool overrideWallCheckCollider = false;
    public Vector2 customWallCheckOffset;
    public Vector2 customWallCheckSize;

    [Header("Model Center Adjustment")]
    // �������� �� ����� pivot �� ����������� ������ ������ (��� �������� ��������� ����������).
    public Vector2 modelCenterOffset;

    [Header("Wall Contact Buffer Settings")]
    // ����� (���), � ������� �������� ������� �� ������ ��������� �����������.
    public float wallContactGracePeriod = 0.15f;
    private float lastWallContactTime = -100f;
    // ������ ������� ���������� ��������: 1 � ���� ����� ������, -1 � ���� �����.
    private int lastWallContactSide = 0;

    // ������� ��������� �������� (���������, ���� ��� ������������ � ������, ��������,
    // ��� ����� ������������ ���������, �� ���������� Hitbox ����� ����������� ������ ������).
    public HitboxState currentHitboxState = HitboxState.Normal;

    // �������� ��� �������: ������� ����� ��� �������� ������������.
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    // ������ ��� �������� ������������ BoxCollider2D � PolygonCollider2D.
    private BoxCollider2D boxCollider;
    private PolygonCollider2D polyCollider;

    void Start()
    {
        // �������� BoxCollider2D � PolygonCollider2D ������ ��� �������� �������� �������� ������������.
        boxCollider = GetComponent<BoxCollider2D>();
        polyCollider = GetComponent<PolygonCollider2D>();
    }

    void Update()
    {
        // ��������� ���� ������ �������� ������������.
        CheckCollisions();
    }

    /// ��������� ������������ � ����� � ������.

    void CheckCollisions()
    {
        // �������� �����: ������������ ������� ������� ����� ��������, ��������� groundCheckOffset.
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundPos, groundCheckSize, 0f, groundLayer);

        // �������� �����: ���������� ����� CheckFullWallContact().
        bool fullContact = CheckFullWallContact();

        // ���� � ���� ����� ������� ���������, ��������� ����� ��������.
        if (fullContact && !IsTouchingWall)
        {
            lastWallContactTime = Time.time;
        }

        // ���� ������� �������� ��� ������� (� ������� wallContactGracePeriod) ��� � �������, ��� ����� ��������.
        IsTouchingWall = fullContact || ((Time.time - lastWallContactTime) <= wallContactGracePeriod);
    }

    /// ��������� �������� �������� �� ������, �������� ����������� ����� �� ����� ��������.

    bool CheckFullWallContact()
    {
        // ���� ���� BoxCollider2D � ���������� ������ ������
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

            // ��������� ����������� ����� ��� ������� � ������ ������ ��������.
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
        // ���� BoxCollider2D ���, �� ���� PolygonCollider2D � ���������� ��� bounds
        else if (polyCollider != null)
        {
            Bounds bounds = polyCollider.bounds;
            float sideOffset = bounds.extents.x * 0.95f;
            float top = bounds.max.y - 0.01f;
            float bottom = bounds.min.y + 0.01f;
            Vector2 pos = (Vector2)transform.position;

            // ��������� ����� �� ����� ��������
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

    /// ���������� ������� ���������� �������� �� ������: 1 � ���� ����� ������, -1 � ���� �����.

    public int GetLastWallContactSide()
    {
        return lastWallContactSide;
    }


    /// ���������� ����� �������� �� ������.

    public void ResetWallContactBuffer()
    {
        lastWallContactTime = -100f;
        lastWallContactSide = 0;
    }

    /// ��������� ������� �� ������.
    public void DetachFromWall()
    {
        IsTouchingWall = false;
        ResetWallContactBuffer();
    }

    void OnDrawGizmosSelected()
    {
        // ������������ ���� �������� �����.
        Gizmos.color = Color.green;
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundPos, groundCheckSize);

        // ������������ ����� �������� �����.
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

    /// ���������, ���� �� ��� ��� �������� �������� (��������, ��� �������� �������).
    public bool IsLedgeClear(Transform ledgeOrigin, float rayLength)
    {
        RaycastHit2D hit = Physics2D.Raycast(ledgeOrigin.position, Vector2.down, rayLength, groundLayer);
        Debug.DrawRay(ledgeOrigin.position, Vector2.down * rayLength, Color.yellow);
        return (hit.collider == null);
    }
}
