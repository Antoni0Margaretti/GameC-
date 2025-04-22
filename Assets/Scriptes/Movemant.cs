using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Параметры цепления за стену (Wall Hang/Slide)
    public float wallSlideMaxSpeed = 5f;
    public float wallSlideAcceleration = 10f;
    public float wallJumpForce = 10f;
    public float wallJumpHorizForce = 5f;
    private bool isSlidingOnWall = false;
    private float timeSinceDetached = 0f;
    public float wallDetachCooldown = 0.3f;

    // --- Гравитация при цеплении
    public float wallHangGravityScale = 0f;
    private float originalGravityScale;

    // --- Направление персонажа
    private bool facingRight = true;

    // --- Переменные для размеров хитбокса
    private Vector2 normalSize;
    private Vector2 normalOffset;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        originalGravityScale = rb.gravityScale;
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");

        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        if (!isSlidingOnWall && !isSliding)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
            if (moveInput > 0 && !facingRight) Flip();
            else if (moveInput < 0 && facingRight) Flip();
        }

        if (Input.GetButtonDown("Jump"))
        {
            if (isSlidingOnWall)
            {
                int wallSide = facingRight ? -1 : 1;
                rb.velocity = new Vector2(wallSide * wallJumpHorizForce, wallJumpForce);
                StopWallSlide();
                timeSinceDetached = 0f;
            }
            else if (grounded || jumpCount < maxJumps)
            {
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                jumpCount++;
            }
        }

        if (grounded)
        {
            jumpCount = 0;
            StopWallSlide();
        }

        if (touchingWall && !grounded && Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash());
        }

        collisionController.ignoreFlipForWallChecks = false;
    }

    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero;
            rb.gravityScale = wallHangGravityScale;
            jumpCount = 0;
        }
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        rb.gravityScale = originalGravityScale;
    }

    private IEnumerator Dash()
    {
        canDash = false;
        float dashDirection = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.velocity = new Vector2(dashDirection * dashSpeed, rb.velocity.y);
        yield return new WaitForSeconds(dashDuration);
        rb.gravityScale = originalGravity;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}