using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Ссылка на компонент коллизий (CollisionController)
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

    // Параметры цепляния за стену (Dead Cells‑подобное поведение)
    public float wallHangTime = 0.5f;      // Время висения на стене
    public float wallSlideSpeed = 2f;      // Скорость скольжения по стене
    public float wallJumpForce = 10f;      // Сила прыжка от стены
    private bool isSlidingOnWall = false;
    private float wallDetachCooldown = 0.2f;  // Повышенный кулдаун, чтобы избежать повторного цепления
    private float timeSinceDetached;

    // Флаг направления (для поворота модели)
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
        float moveInput = Input.GetAxis("Horizontal");

        // Если не цепляемся за стену, не выполняем подкат и не приседаем – обычное движение:
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Обновляем направление персонажа, если он не в цеплянии. 
            // Однако, даже когда цепляемся за стену, мы хотим, чтобы при наличии ввода A/D модель менялась.
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        // Если цепляемся за стену – разрешаем смену направления на основе ввода,
        // чтобы при wall jump персонаж уже смотрел в нужную сторону.
        else if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // Прыжок (при условии, что персонаж на земле, или не исчерпал прыжки, или находится в состоянии цепления)
        if (Input.GetButtonDown("Jump") && (collisionController.IsGrounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

            // Если персонаж цепляется за стену – выполнять wall jump:
            if (isSlidingOnWall)
            {
                rb.velocity = new Vector2(-Mathf.Sign(transform.localScale.x) * speed, wallJumpForce);
                isSlidingOnWall = false;
                timeSinceDetached = 0f;
            }

            jumpCount++;
        }

        // Сброс прыжков при касании земли
        if (collisionController.IsGrounded)
        {
            jumpCount = 0;
            isSlidingOnWall = false;
        }

        // Логика цепления за стену:
        // Добавлено условие, что wall hang начинается только если вертикальная скорость <= 0 (персонаж уже падает)
        if (collisionController.IsTouchingWall && !collisionController.IsGrounded &&
            timeSinceDetached > wallDetachCooldown && rb.velocity.y <= 0)
        {
            StartWallHang();
        }

        // Отмена цепления (скольжения) по стене при нажатии S
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
        // Если персонаж движется на земле – подкат.
        if (Input.GetKeyDown(KeyCode.LeftControl) && collisionController.IsGrounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        // Если персонаж стоит на месте и зажат LeftControl – приседание (аналогично S)
        else if (Input.GetKey(KeyCode.LeftControl) && collisionController.IsGrounded && Mathf.Abs(moveInput) < 0.01f)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero;
            }
        }
        // Приседание также по S
        else if (Input.GetKey(KeyCode.S) && collisionController.IsGrounded && !isSliding && !isSlidingOnWall)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero;
            }
        }
        else if (!Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.LeftControl))
        {
            isCrouching = false;
        }
    }

    // Методы цепления за стену
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero; // Полная остановка
            Invoke("BeginWallSlide", wallHangTime);
        }
    }

    private void BeginWallSlide()
    {
        // Входим в состояние скольжения по стене
        isSlidingOnWall = true;
        rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        timeSinceDetached = 0f;
    }

    // Рывок
    private IEnumerator Dash(float moveInput)
    {
        canDash = false;
        float dashDirection = moveInput != 0 ? Mathf.Sign(moveInput) : (facingRight ? 1 : -1);
        Vector2 dashVector = new Vector2(dashDirection * dashDistance, 0);
        rb.velocity = dashVector;
        yield return new WaitForSeconds(0.1f);
        rb.velocity = new Vector2(0, rb.velocity.y);
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // Подкат
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
    }

    // Метод для поворота персонажа
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}
