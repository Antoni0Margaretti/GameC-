using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Переменные для движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // Переменные для приседания
    private bool isCrouching = false;

    // Переменные для рывка
    public float dashDistance = 5f;
    public float dashCooldown = 1f;
    private bool canDash = true;

    // Переменные для подката
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // Переменные для стены
    public float wallSlideSpeed = 2f;
    public float wallStickTime = 0.5f;
    private bool isTouchingWall;
    private bool isWallSliding;
    private bool canWallJump = true;

    // Прочие компоненты
    private Rigidbody2D rb;
    private Animator animator;

    // Слои для проверки
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Проверяем, что персонаж не в состоянии подката
        if (!isSliding && !isCrouching)
        {
            // Бег
            float moveInput = Input.GetAxis("Horizontal");
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Анимация бега
            animator.SetFloat("Speed", Mathf.Abs(moveInput));

            // Поворот персонажа
            if (moveInput > 0)
            {
                transform.localScale = new Vector3(1, 1, 1);
            }
            else if (moveInput < 0)
            {
                transform.localScale = new Vector3(-1, 1, 1);
            }
        }

        // Прыжок
        if (Input.GetButtonDown("Jump") && (IsGrounded() || jumpCount < maxJumps))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpCount++;
            animator.SetTrigger("Jump");
        }

        // Сброс прыжков при касании земли
        if (IsGrounded())
        {
            jumpCount = 0;
        }

        // Приседание
        if (Input.GetKey(KeyCode.S) && IsGrounded())
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero; // Полная остановка
                animator.SetBool("Crouch", true);
            }
        }
        else if (isCrouching)
        {
            isCrouching = false;
            animator.SetBool("Crouch", false);
        }

        // Рывок
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash(Input.GetAxis("Horizontal")));
        }

        // Цепляние за стену
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

        // Отпрыгивание от стены
        if (Input.GetButtonDown("Jump") && isWallSliding)
        {
            rb.velocity = new Vector2(-Mathf.Sign(transform.localScale.x) * speed, jumpForce);
        }

        // Подкат
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSliding && IsGrounded())
        {
            StartCoroutine(Slide(Input.GetAxis("Horizontal")));
        }
    }

    // Проверка на землю
    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(transform.position, 0.1f, groundLayer);
    }

    // Проверка на стену
    private bool IsTouchingWall()
    {
        return Physics2D.OverlapCircle(transform.position, 0.1f, wallLayer);
    }

    // Рывок
    private System.Collections.IEnumerator Dash(float moveInput)
    {
        canDash = false;
        Vector2 dashVector = new Vector2(moveInput * dashDistance, rb.velocity.y);
        rb.velocity = dashVector;
        yield return new WaitForSeconds(0.1f); // Длительность рывка
        rb.velocity = Vector2.zero; // Обнуление скорости
        yield return new WaitForSeconds(dashCooldown); // Восстановление рывка
        canDash = true;
    }

    // Подкат
    private System.Collections.IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        rb.velocity = new Vector2(moveInput * slideSpeed, rb.velocity.y);
        animator.SetTrigger("Slide");
        yield return new WaitForSeconds(slideDuration);
        isSliding = false;
    }
}