using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Ссылка на компонент коллизий
    private CollisionController collisionController;

    // Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // Параметры рывка
    public float dashDistance = 5f;
    public float dashCooldown = 1f;
    private bool canDash = true;

    // Параметры подката
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // Параметры приседания
    private bool isCrouching = false;

    // Параметры цепления за стену (Dead Cells‑подобное поведение)
    public float wallHangTime = 0.5f;       // Время, которое персонаж висит на стене перед скольжением
    public float wallSlideSpeed = 2f;       // Скорость скольжения по стене
    public float wallJumpForce = 10f;       // Сила прыжка от стены
    private bool isSlidingOnWall = false;
    // Время, в течение которого персонаж не может повторно зацепиться за стену после wall jump
    public float wallDetachCooldown = 0.3f;
    private float timeSinceDetached;

    // Флаг направления (определяет, куда смотрит персонаж)
    private bool facingRight = true;

    // Компонент Rigidbody2D
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;

        // Кэшируем значения коллизий
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;
        float moveInput = Input.GetAxis("Horizontal");

        // Обработка обычного движения (если не цепляемся, не в подкате и не приседаем)
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

            // Обновляем направление персонажа на основе ввода
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        // Если персонаж цепляется за стену, всё равно отслеживаем ввод для смены направления
        else if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // Обработка прыжка:
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

            // Если персонаж отцеплен от стены прыжком – выполняется wall jump
            if (isSlidingOnWall)
            {
                rb.linearVelocity = new Vector2(-(facingRight ? 1 : -1) * speed, wallJumpForce);
                isSlidingOnWall = false;
                timeSinceDetached = 0f;
            }
            jumpCount++;
        }

        // При касании земли сбрасываем прыжки и состояние цепления
        if (grounded)
        {
            jumpCount = 0;
            isSlidingOnWall = false;
        }

        // Логика цепления за стену:
        // Если персонаж касается стены, находится в воздухе, и ввод соответствует направлению, в которое он смотрит,
        // а также прошло достаточно времени после wall jump – начинаем wall hang.
        if (touchingWall && !grounded &&
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached > wallDetachCooldown)
        {
            StartWallHang();
        }

        // Отмена цепления по клавише S
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // Рывок (LeftShift)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash(moveInput));
        }

        // Обработка клавиши LeftControl:
        // Если персонаж движется на земле – выполняем подкат.
        if (Input.GetKeyDown(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        // Если персонаж стоит на месте и зажат LeftControl – режим приседания (аналог S)
        else if (Input.GetKey(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.linearVelocity = Vector2.zero;
            }
        }
        // Приседание также с клавишей S
        else if (Input.GetKey(KeyCode.S) && grounded && !isSliding && !isSlidingOnWall)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.linearVelocity = Vector2.zero;
            }
        }
        else if (!Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.LeftControl))
        {
            isCrouching = false;
        }

        // Если цепляемся за стену, делаем так, чтобы хитбокс стены не сдвигался (фиксируем зону коллизии)
        collisionController.ignoreFlipForWallChecks = isSlidingOnWall;
    }

    // Wall Hang: начинается цепление за стену
    // Сбрасываем счётчик прыжков для обновления двойного прыжка
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.linearVelocity = Vector2.zero;
            jumpCount = 0;
            StartCoroutine(WallHangCoroutine());
        }
    }

    // Coroutine для ожидания перед началом скольжения по стене
    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        // Если до сих пор цепление активно, начинаем скольжение
        if (isSlidingOnWall)
        {
            BeginWallSlide();
        }
    }

    // Начало скольжения по стене
    private void BeginWallSlide()
    {
        isSlidingOnWall = true;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
    }

    // Прекращение цепления
    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        timeSinceDetached = 0f;
    }

    // Рывок
    private IEnumerator Dash(float moveInput)
    {
        canDash = false;
        float dashDirection = Mathf.Abs(moveInput) > 0.01f ? Mathf.Sign(moveInput) : (facingRight ? 1 : -1);
        Vector2 dashVector = new Vector2(dashDirection * dashDistance, 0);
        rb.linearVelocity = dashVector;
        yield return new WaitForSeconds(0.1f);
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // Подкат
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.linearVelocity = new Vector2(slideDirection * slideSpeed, rb.linearVelocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.linearVelocity = Vector2.zero;
        isSliding = false;
    }

    // Метод для инверсии направления персонажа
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}