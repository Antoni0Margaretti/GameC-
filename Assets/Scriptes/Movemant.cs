using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // ���������� ��� ��������
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // ���������� ��� ����������
    private bool isCrouching = false;

    // ���������� ��� �����
    public float dashDistance = 5f;
    public float dashCooldown = 1f;
    private bool canDash = true;

    // ���������� ��� �������
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // ���������� ��� �����
    public float wallSlideSpeed = 2f;
    public float wallStickTime = 0.5f;
    private bool isTouchingWall;
    private bool isWallSliding;
    private bool canWallJump = true;

    // ������ ����������
    private Rigidbody2D rb;
    private Animator animator;

    // ���� ��� ��������
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // ���������, ��� �������� �� � ��������� �������
        if (!isSliding && !isCrouching)
        {
            // ���
            float moveInput = Input.GetAxis("Horizontal");
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // �������� ����
            animator.SetFloat("Speed", Mathf.Abs(moveInput));

            // ������� ���������
            if (moveInput > 0)
            {
                transform.localScale = new Vector3(1, 1, 1);
            }
            else if (moveInput < 0)
            {
                transform.localScale = new Vector3(-1, 1, 1);
            }
        }

        // ������
        if (Input.GetButtonDown("Jump") && (IsGrounded() || jumpCount < maxJumps))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpCount++;
            animator.SetTrigger("Jump");
        }

        // ����� ������� ��� ������� �����
        if (IsGrounded())
        {
            jumpCount = 0;
        }

        // ����������
        if (Input.GetKey(KeyCode.S) && IsGrounded())
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero; // ������ ���������
                animator.SetBool("Crouch", true);
            }
        }
        else if (isCrouching)
        {
            isCrouching = false;
            animator.SetBool("Crouch", false);
        }

        // �����
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash(Input.GetAxis("Horizontal")));
        }

        // �������� �� �����
        isTouchingWall = IsTouchingWall();
        if (isTouchingWall && !IsGrounded() && Input.GetAxis("Horizontal") != 0)
        {
            isWallSliding = true;
            rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
            animator.SetBool("WallSlide", true);
        }
        else
        {
            isWallSliding = false;
            animator.SetBool("WallSlide", false);
        }

        // ������������ �� �����
        if (Input.GetButtonDown("Jump") && isWallSliding)
        {
            rb.velocity = new Vector2(-Mathf.Sign(transform.localScale.x) * speed, jumpForce);
        }

        // ������
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSliding && IsGrounded())
        {
            StartCoroutine(Slide(Input.GetAxis("Horizontal")));
        }
    }

    // �������� �� �����
    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(transform.position, 0.1f, groundLayer);
    }

    // �������� �� �����
    private bool IsTouchingWall()
    {
        return Physics2D.OverlapCircle(transform.position, 0.1f, wallLayer);
    }

    // �����
    private System.Collections.IEnumerator Dash(float moveInput)
    {
        canDash = false;
        Vector2 dashVector = new Vector2(moveInput * dashDistance, rb.velocity.y);
        rb.velocity = dashVector;
        yield return new WaitForSeconds(0.1f); // ������������ �����
        rb.velocity = Vector2.zero; // ��������� ��������
        yield return new WaitForSeconds(dashCooldown); // �������������� �����
        canDash = true;
    }

    // ������
    private System.Collections.IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        rb.velocity = new Vector2(moveInput * slideSpeed, rb.velocity.y);
        animator.SetTrigger("Slide");
        yield return new WaitForSeconds(slideDuration);
        isSliding = false;
    }
}