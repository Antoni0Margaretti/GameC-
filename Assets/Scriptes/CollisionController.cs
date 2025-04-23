using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    // �������� � ������ ���� �������� �����.
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    // ���� true � ���������� ������� (flip) ��� �������� �����.
    public bool ignoreFlipForWallChecks = false;

    [Header("Wall Check Collider Settings (Override)")]
    // ���� ����������� � true, ��� �������� �������� �� ����� ������������ ��������� ����.
    public bool overrideWallCheckCollider = false;
    public Vector2 customWallCheckOffset;
    public Vector2 customWallCheckSize;

    [Header("Model Center Adjustment")]
    // �������� �� ����� pivot �� ����������� ������ ������.
    // ��� �������� ��� �������� ��������� ����������.
    public Vector2 modelCenterOffset;

    [Header("Wall Contact Buffer Settings")]
    // ����� ������� (� ��������), � ������� �������� ������� �� ������ ��������� �����������,
    // ���� ���� ������� �������� �� ���������� ������������.
    public float wallContactGracePeriod = 0.15f;
    // ����� ���������� ������������� �������� �� ������.
    private float lastWallContactTime = -100f;

    // �������� ��� ������� �� ������ ��������.
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
        // �������� ������� ������� �������� �����.
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0f, groundLayer);

        // �������� ��������� ������ �������� �����.
        bool fullContact = CheckFullWallContact();
        // ���� ��������� ������� � ��������� ����� ��������.
        if (fullContact)
        {
            lastWallContactTime = Time.time;
        }
        // ���� � ������� ���������� ������������� �������� ������ �� ����� wallContactGracePeriod, �������,
        // ��� �������� �� ��� ��������� � �����.
        IsTouchingWall = (Time.time - lastWallContactTime) <= wallContactGracePeriod;
    }

    // ���������� true, ���� ���� �� ���� �� ����� (������� ��� �����) ��������� ��������� � �����.
    private bool CheckFullWallContact()
    {
        // ��������� ������� � ������ �������� ������ ������.
        Vector2 pos = (Vector2)transform.position +
                      new Vector2(ignoreFlipForWallChecks ? modelCenterOffset.x : (transform.localScale.x >= 0 ? modelCenterOffset.x : -modelCenterOffset.x),
                                  modelCenterOffset.y);

        // �������� offset � ������ ��� ��������: ���� �� BoxCollider2D, ���� �� �������� override.
        Vector2 offset = overrideWallCheckCollider ? customWallCheckOffset : boxCollider.offset;
        Vector2 size = overrideWallCheckCollider ? customWallCheckSize : boxCollider.size;
        Vector2 halfSize = size * 0.5f;

        bool facingRight = ignoreFlipForWallChecks ? true : (transform.localScale.x >= 0);
        Vector2 frontTop, frontBottom, backTop, backBottom;

        if (facingRight)
        {
            // ���� �������� ������� ������, "����� �������" � ������ �������.
            frontTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
        }
        else
        {
            // ���� �������� ������� �����, "����� �������" � ����� �������.
            frontTop = pos + offset + new Vector2(-halfSize.x, halfSize.y);
            frontBottom = pos + offset + new Vector2(-halfSize.x, -halfSize.y);
            backTop = pos + offset + new Vector2(halfSize.x, halfSize.y);
            backBottom = pos + offset + new Vector2(halfSize.x, -halfSize.y);
        }
        // ���� ��� ����������� ����� ����� �� ����� �������� ������� �� ���� wallLayer, �������, ��� ����� ��������� ���������.
        bool frontFull = Physics2D.OverlapPoint(frontTop, wallLayer) && Physics2D.OverlapPoint(frontBottom, wallLayer);
        bool backFull = Physics2D.OverlapPoint(backTop, wallLayer) && Physics2D.OverlapPoint(backBottom, wallLayer);
        return frontFull || backFull;
    }

    void OnDrawGizmosSelected()
    {
        // ��������� ���� �������� �����.
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        // ���� ������������ BoxCollider2D � ������������ ����� ��� �������� �����.
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
