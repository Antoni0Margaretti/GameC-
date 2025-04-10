using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Переменные для движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // Переменные для рывка
    public float dashDistance = 5f;
    public float dashCooldown = 1f;
    private bool canDash = true;

    // Переменные для подката
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // Переменные для стены
    public float wallHangTime = 0.5f;
    public float wallSlideSpeed = 2f;
    public float wallJumpForce = 10f;
    private bool isTouchingWall;
    private bool isSlidingOnWall;
    private float wallDetachCooldown = 0.1f;
    private float timeSinceDetached;

    // Прочие компоненты
    private Rigidbody2D rb;

    // Слои для проверки
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;

        // Основное движение персонажа
        float moveInput = Input.GetAxis("Horizontal");
        if (!isSlidingOnWall && !isSliding)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
        }

        // Прыжок
        if (Input.GetButtonDown("Jump") && (IsGrounded() || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

            // Если персонаж скользит по стене, прыжок отталкивает его
            if (isSlidingOnWall)
            {
                rb.velocity = new Vector2(-Mathf.Sign(transform.localScale.x) * speed, wallJumpForce);
                isSlidingOnWall = false;
                timeSinceDetached = 0f;
            }

            jumpCount++;
        }

        // Сброс прыжков при касании земли
        if (IsGrounded())
        {
            jumpCount = 0; // Обновление прыжков
            isSlidingOnWall = false; // Скользящее состояние сбрасывается
        }

        // Рывок
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash(moveInput));
        }

        // Подкат
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSliding && IsGrounded() && moveInput != 0)
        {
            StartCoroutine(Slide(moveInput));
        }
    }

    // Рывок
    private System.Collections.IEnumerator Dash(float moveInput)
    {
        canDash = false;

        // Вычисляем направление рывка
        float dashDirection = moveInput != 0 ? Mathf.Sign(moveInput) : (transform.localScale.x > 0 ? 1 : -1);
        Vector2 dashVector = new Vector2(dashDirection * dashDistance, 0);

        // Выполняем рывок
        rb.velocity = dashVector;
        yield return new WaitForSeconds(0.1f); // Длительность рывка

        rb.velocity = new Vector2(0, rb.velocity.y); // Сбрасываем горизонтальную скорость
        yield return new WaitForSeconds(dashCooldown); // Время на восстановление

        canDash = true;
    }

    // Подкат
    private System.Collections.IEnumerator Slide(float moveInput)
    {
        isSliding = true;

        // Устанавливаем скорость для подката
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);

        yield return new WaitForSeconds(slideDuration);

        rb.velocity = Vector2.zero; // Сбрасываем скорость после подката
        isSliding = false;
    }

    // Проверка на землю
    private bool IsGrounded()
    {
        Vector2 position = transform.position;
        float radius = 0.25f; // Увеличен радиус проверки
        bool grounded = Physics2D.OverlapCircle(position, radius, groundLayer);

        // Отладка
        if (grounded)
        {
            Debug.Log("Персонаж касается земли");
        }

        return grounded;
    }

    // Проверка на стену
    private bool IsTouchingWall()
    {
        Vector2 position = transform.position;
        float radius = 0.2f;
        return Physics2D.OverlapCircle(position, radius, wallLayer);
    }
}