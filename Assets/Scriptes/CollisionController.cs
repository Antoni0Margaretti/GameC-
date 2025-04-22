using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("Ground Check Settings")]
    public LayerMask groundLayer;
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    public Vector2 wallCheckOffset = new Vector2(0.4f, 0);
    public Vector2 wallCheckSize = new Vector2(0.2f, 1f);
    public bool ignoreFlipForWallChecks = false;

    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    void Update()
    {
        // �������� �����
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundPos, groundCheckSize, 0f, groundLayer);

        // �������� �����
        Vector2 wallPos = ignoreFlipForWallChecks ?
            ((Vector2)transform.position + wallCheckOffset) :
            (Vector2)transform.TransformPoint(wallCheckOffset);
        IsTouchingWall = Physics2D.OverlapBox(wallPos, wallCheckSize, 0f, wallLayer);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 groundPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundPos, groundCheckSize);

        Gizmos.color = Color.red;
        Vector2 wallPos = ignoreFlipForWallChecks ?
            ((Vector2)transform.position + wallCheckOffset) :
            (Vector2)transform.TransformPoint(wallCheckOffset);
        Gizmos.DrawWireCube(wallPos, wallCheckSize);
    }

    // ����� ����� � ����������, ��������� �� �������� � ����������� (������) ���� �����.
    public bool IsInSoftWallZone(float softMargin)
    {
        Vector2 origin = (Vector2)transform.TransformPoint(wallCheckOffset);
        // ��������� ������ ���� �������� �� ����������� �� softMargin � ����� ������.
        Vector2 softSize = wallCheckSize + new Vector2(softMargin * 2, 0);
        // ���������� �����������, ��������� localScale.x: ���� > 0 � ������, ���� < 0 � �����.
        Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        RaycastHit2D hit = Physics2D.BoxCast(origin, softSize, 0f, direction, softMargin, wallLayer);
        return hit.collider != null;
    }

    // ����� ����� � ������������� ��������� ������ (snap � �����) ��� �������� �� ������ ����.
    public void SnapToWall(Transform playerTransform, float softMargin)
    {
        Vector2 origin = (Vector2)transform.TransformPoint(wallCheckOffset);
        Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        // ���������� ����������, ������� softMargin � ����������� ������� ����.
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, softMargin * 5, wallLayer);
        if (hit.collider != null)
        {
            float colliderExtent = wallCheckSize.x * 0.5f;
            float desiredX = direction == Vector2.right ?
                hit.point.x - (colliderExtent + 0.05f) :
                hit.point.x + (colliderExtent + 0.05f);
            playerTransform.position = new Vector2(desiredX, playerTransform.position.y);
        }
    }
}