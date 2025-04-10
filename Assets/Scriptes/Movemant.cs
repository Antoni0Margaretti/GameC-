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

    // Переменные для приседания
    private bool isCrouching = false;

    // Переменные для стены
    public float wallHangTime = 0.5f;      // Время висения на стене
    public float wallSlideSpeed = 2f;     // Скорость скольжения по стене
    public float wallJumpForce = 10f;    // Сила прыжка от стены
    private bool isTouchingWall;          // Проверка касания стены
    private bool isSlidingOnWall;         // Состояние скольжения по стене
    private float wallDetachCooldown = 0.1f; // Время между повторными цепляниями
    private float timeSinceDetached;      // Время с момента отлипания от стены

    // Флаг направления персонажа
    private bool facingRight = true;

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
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Обновляем направление персонажа
            if (moveInput > 0 && !facingRight)
            {
                Flip();
            }
            else if (moveInput < 0 && facingRight)
            {
                Flip();
            }
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

        // Логика стены: цепляние, висение и скольжение
        isTouchingWall = IsTouchingWall();
        if (isTouchingWall && !IsGrounded() && timeSinceDetached > wallDetachCooldown)
        {
            StartWallHang(); // Начало висения на стене
        }

        // Отмена скольжения при падении, если нажать S
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // Рывок
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash(moveInput));
        }

        // Подкат
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSliding && IsGrounded())
        {
            StartCoroutine(Slide(moveInput));
        }

        // Приседание
        if (Input.GetKey(KeyCode.S) && IsGrounded() && !isSliding && !isSlidingOnWall)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero; // Полная остановка
            }
        }
        else if (isCrouching)
        {
            isCrouching = false;
        }
    }

    // Цепляние за стену (висение)
    private void StartWallHang()
    {
        if (!isSlidingOnWall) // Проверка, чтобы цепляние происходило только один раз
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero; // Полная остановка при цеплянии
            Invoke("BeginWallSlide", wallHangTime); // Через wallHangTime начнётся скольжение
        }
    }

    // Начало скольжения
    private void BeginWallSlide()
    {
        isSlidingOnWall = true; // Переход в состояние скольжения
        rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed); // Начальная скорость скольжения
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        timeSinceDetached = 0f; // Отсчёт времени для повторного цепляния
    }

    // Рывок
    private System.Collections.IEnumerator Dash(float moveInput)
    {
        if (moveInput != 0 && canDash)
        {
            canDash = false;
            Vector2 dashVector = new Vector2(Mathf.Sign(moveInput) * dashDistance, 0);
            rb.velocity = dashVector;
            yield return new WaitForSeconds(0.1f); // Длительность рывка
            rb.velocity = new Vector2(0, rb.velocity.y); // Обнуление горизонтальной скорости
            yield return new WaitForSeconds(dashCooldown); // Ожидание перед следующим рывком
            canDash = true;
        }
    }

    // Подкат
    private System.Collections.IEnumerator Slide(float moveInput)
    {
        if (moveInput != 0 && IsGrounded())
        {
            isSliding = true;
            rb.velocity = new Vector2(Mathf.Sign(moveInput) * slideSpeed, rb.velocity.y);
            yield return new WaitForSeconds(slideDuration);
            rb.velocity = Vector2.zero; // Полная остановка
            isSliding = false;
        }
    }

    // Проверка на землю
    private bool IsGrounded()
    {
        Vector2 position = transform.position;
        float radius = 0.2f;
        return Physics2D.OverlapCircle(position, radius, groundLayer);
    }

    // Проверка на стену
    private bool IsTouchingWall()
    {
        Vector2 position = transform.position;
        float radius = 0.2f;
        return Physics2D.OverlapCircle(position, radius, wallLayer);
    }

    // Метод для поворота персонажа
    private void Flip()
    {
        facingRight = !facingRight; // Меняем направление персонажа
        Vector3 newScale = transform.localScale;
        newScale.x *= -1; // Инвертируем ось X для поворота
        transform.localScale = newScale;
    }
}
