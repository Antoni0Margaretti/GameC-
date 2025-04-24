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

    [Header("Dynamic Hitbox Settings")]
    // ������������� �������� ��� ��������� ���������.
    public Vector2 normalHitboxSize;
    public Vector2 normalHitboxOffset;
    public Vector2 crouchingHitboxSize;
    public Vector2 crouchingHitboxOffset;
    public Vector2 slidingHitboxSize;
    public Vector2 slidingHitboxOffset;

    // ������� ��������� ��������; ��� ����� ������ �� PlayerController.
    public HitboxState currentHitboxState = HitboxState.Normal;

    // �������� ��� ������� �����.
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    private BoxCollider2D boxCollider;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        // ���� ������������ �������� �� ������ ����� Inspector, ���������� ��������� BoxCollider2D.
        if (normalHitboxSize == Vector2.zero) normalHitboxSize = boxCollider.size;
        if (normalHitboxOffset == Vector2.zero) normalHitboxOffset = boxCollider.offset;
    }

    void Update()
    {
        UpdateHitbox();
        CheckCollisions();
    }

    /// <summary>
    /// ��������� ������� � �������� BoxCollider2D � ����������� �� ��������� ��������.
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
    /// ��������� ������������ � ����� � �� ������.
    /// </summary>
    void CheckCollisions()
    {
        // �������� �����.
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundPos, groundCheckSize, 0f, groundLayer);

        // �������� �����: ���������, ��������� �� �������.
        bool fullContact = CheckFullWallContact();

        // ���� � ���� ����� ������� ���������, �� � ���������� ����� ��� �� ����,
        // ��������� ����� ��������.
        if (fullContact && !IsTouchingWall)
        {
            lastWallContactTime = Time.time;
        }

        // ���� ������� ���������, ��� ������� ������� (� �������� grace period),
        // �������, ��� �������� �� ��� ���������.
        IsTouchingWall = fullContact || ((Time.time - lastWallContactTime) <= wallContactGracePeriod);
    }

    bool CheckFullWallContact()
    {
        // ��������� ������� ������� � ��������� �� modelCenterOffset.
        Vector2 pos = (Vector2)transform.position +
                      new Vector2(
                          ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                          modelCenterOffset.y);

        // ���������� offset � ������ ��� �������� � ���� ���������, ���� �� BoxCollider2D.
        Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : boxCollider.offset;
        Vector2 size = overrideWallCheckCollider ? customWallCheckSize : boxCollider.size;
        Vector2 halfSize = size * 0.5f;

        bool facingRight = ignoreFlipForWallChecks ? true : (transform.localScale.x >= 0);

        // ���������� ����������� ����� ��� ������� � ������ ����� ��������.
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
    /// ���������� ������� ���������� �������� �� ������: 1 � ���� ����� ������, -1 � ���� �����.
    /// </summary>
    public int GetLastWallContactSide()
    {
        return lastWallContactSide;
    }

    /// <summary>
    /// ���������� ����� �������� �� ������.
    /// </summary>
    public void ResetWallContactBuffer()
    {
        lastWallContactTime = -100f;
        lastWallContactSide = 0;
    }

    void OnDrawGizmosSelected()
    {
        // ��������� ���� �������� �����.
        Gizmos.color = Color.green;
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundPos, groundCheckSize);

        // ��������� ����� �������� �����.
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
}