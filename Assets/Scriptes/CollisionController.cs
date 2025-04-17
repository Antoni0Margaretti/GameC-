using UnityEngine;

public class CollisionController : MonoBehaviour
{
    [Header("��������� �������� �����")]
    public LayerMask groundLayer;
    // �������� ��� �������� ����� � ��������� ����������� (��������, (0, -0.5f))
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);

    [Header("��������� �������� �����")]
    public LayerMask wallLayer;
    // �������� ��� �������� ����� � ��������� ����������� (��������, (0.4f, 0))
    public Vector2 wallCheckOffset = new Vector2(0.4f, 0);
    public Vector2 wallCheckSize = new Vector2(0.2f, 1.0f);

    // �������� ��� ������� �� ������� �������
    public bool IsGrounded { get; private set; }
    public bool IsTouchingWall { get; private set; }

    void Update()
    {
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        // ����������� ��������� �������� � ������� ����������.
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        IsGrounded = Physics2D.OverlapBox(groundCheckPos, groundCheckSize, 0, groundLayer);

        Vector2 wallCheckPos = (Vector2)transform.TransformPoint(wallCheckOffset);
        IsTouchingWall = Physics2D.OverlapBox(wallCheckPos, wallCheckSize, 0, wallLayer);
    }

    // ��� ���������� ������� � ����������� ��� ��������.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 groundCheckPos = (Vector2)transform.TransformPoint(groundCheckOffset);
        Gizmos.DrawWireCube(groundCheckPos, groundCheckSize);

        Gizmos.color = Color.red;
        Vector2 wallCheckPos = (Vector2)transform.TransformPoint(wallCheckOffset);
        Gizmos.DrawWireCube(wallCheckPos, wallCheckSize);
    }
}
