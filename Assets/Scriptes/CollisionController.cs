using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- ����������
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- ��������� ��������
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- ��������� ����� (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // ������������ �����
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- ��������� ������� (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- ���������� (��� dash �� �����������)
    private bool isCrouching = false;

    // --- ��������� �������� �� ����� (Wall Hang/Slide)
    public float wallHangTime = 0.5f;    // �����, ������� �������� ����� ���������� ����� ��������
    public float wallSlideAcceleration = 10f;  // ��������� ���������� �� �����
    public float wallSlideMaxSpeed = 5f;       // ������������ �������� ����������
    public float wallJumpForce = 10f;    // ������������ ���������� wall jump
    public float wallJumpHorizForce = 5f; // �������������� ������������ wall jump
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;
    public float wallDetachCooldown = 0.3f;   // �����, � ������� �������� ������ �������� ���������� �� �� �� �����
    private float timeSinceDetached = 0f;

    // --- ��������� ���������� ��� ��������
    public float wallHangGravityScale = 0f;   // �������� gravityScale �� ����� ��������
    private float originalGravityScale;

    // --- ���� ����������� (���� ������� ��������)
    private bool facingRight = true;

    // --- ������� ����� ��� ��������: 1, ���� ����� ������; -1, ���� �����.
    private int wallSide = 0;

    // --- �����: ��������� ��� ���������� ������������� �������� ����� wall jump
    public float wallJumpDisableDuration = 0.5f; // ������� ������ ������������ �������� ����� wall jump
    private bool skipWallCollision = false;      // ���� true, �� �������� ��������� �� �����

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // ��������� �������� �������� ����������
        originalGravityScale = rb.gravityScale;
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");

        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // ���� �������� ��������� �� �����, �� ����� �������� � ���������� ��������.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // ��� ��������, ���� ������������ ������ ��� ����� �����������.
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        else if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            // ������� ��������
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- ������
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            if (isSlidingOnWall)
            {
                // ���������� ������� ��������:
                // ���� ����� ����� ���, �������, ��� �������� ��� ��������� �� �����;
                // ����� ���������� �� �����.
                if (Mathf.Abs(moveInput) < 0.01f)
                    wallSide = (facingRight) ? -1 : 1;
                else
                    wallSide = (moveInput > 0) ? 1 : -1;

                // ���� �������� ��� �������� �� ����� (�� ���� ��� ������ �������������� wallSide),
                // ��������� wall jump � �������������� ��������� (��� ��������������� �����)
                if ((facingRight && wallSide < 0) || (!facingRight && wallSide > 0))
                {
                    WallJump();
                }
                else
                {
                    // ����� ������� ������������ ������.
                    NormalJump();
                    jumpCount++;
                }
            }
            else
            {
                NormalJump();
                jumpCount++;
            }
        }
        if (grounded)
        {
            jumpCount = 0;
            StopWallSlide();
        }

        // --- ������������� �������� �� ����� (Wall Hang)
        // �� �������� ��������, ���� skipWallCollision �����������.
        if (!skipWallCollision && collisionController.IsTouchingWall && !grounded &&
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // ��������� wall slide ������ ���� �� � ��������� wall jump.
        if (isSlidingOnWall && wallSlideActive && touchingWall && !skipWallCollision)
        {
            float newY = Mathf.MoveTowards(rb.velocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.velocity = new Vector2(rb.velocity.x, newY);
        }

        // --- ��������� �������� (Dash, Slide, Crouch)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }
        if (Input.GetKeyDown(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            isCrouching = true;
            rb.velocity = Vector2.zero;
        }
        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
        {
            isCrouching = false;
        }
    }

    // --- ���������� ������ (������������)
    private void NormalJump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    // --- Wall Jump: ������������ �� ����� � ��������� ����������.
    private void WallJump()
    {
        rb.velocity = new Vector2(-wallSide * wallJumpHorizForce, wallJumpForce);
        jumpCount = 0;
        StopWallSlide();
        timeSinceDetached = 0f;

        // ������������� ���� ���������� �������� �� ����� wallJumpDisableDuration.
        skipWallCollision = true;
        StartCoroutine(ResetWallCollision());
    }

    private IEnumerator ResetWallCollision()
    {
        yield return new WaitForSeconds(wallJumpDisableDuration);
        skipWallCollision = false;
    }

    // --- ������ �������� �� ����� (Wall Hang)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;  // ������� ����������� �������
            rb.velocity = Vector2.zero;
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;
        }
    }

    // --- ����������� �������� �� ����� (Wall Slide)
    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
        rb.gravityScale = originalGravityScale;
    }

    // --- Dash
    private IEnumerator Dash()
    {
        canDash = false;
        float currentVertical = rb.velocity.y;
        if (collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);
            currentVertical = 0;
        }
        float dashDir = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;
        float origGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.velocity = new Vector2(dashDir * dashSpeed, currentVertical);
        yield return new WaitForSeconds(dashDuration);
        rb.gravityScale = origGravity;
        if (collisionController.IsGrounded)
            rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // --- Slide
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDir = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDir * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
    }

    // --- Flip: ������ ����������� �������
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}