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

    // Параметры цепления за стену (Dead Cells‑подобное поведение)
    public float wallHangTime = 0.5f;       // Время, в течение которого персонаж висит на стене
    public float wallSlideSpeed = 2f;       // Скорость скольжения по стене
    public float wallJumpForce = 10f;       // Сила прыжка от стены
    private bool isSlidingOnWall = false;
    // Время, в течение которого персонаж не может заново зацепиться за стену после wall jump.
    public float wallDetachCooldown = 0.3f;
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

        // Если персонаж не цепляется за стену, не в подкате и не приседает – обычное движение:
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Обновляем направление персонажа
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        // Даже если персонаж цепляется за стену, позволяйте менять направление по вводимым клавишам.
        else if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // Прыжок. Условие: персонаж на земле, или еще не исчерпал прыжки, или уже цепляется за стену.
        if (Input.GetButtonDown("Jump") && (collisionController.IsGrounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

            // Если персонаж цепляется за стену – выполняем wall jump:
            if (isSlidingOnWall)
            {
                // Отталкиваем от стены, используя знак текущего направления
                rb.velocity = new Vector2(-Mathf.Sign(transform.localScale.x) * speed, wallJumpForce);
                isSlidingOnWall = false;
                timeSinceDetached = 0f; // Запускаем таймер "отлипания" от стены
            }

            jumpCount++;
        }

        // Сброс прыжков при касании земли
        if (collisionController.IsGrounded)
        {
            jumpCount = 0;
            isSlidingOnWall = false;
        }

        /* 
         * Логика цепления за стену:
         * Если персонаж касается стены, находится в воздухе и действует идущая клавиша в направлении стены,
         * то начинается цепление.
         *
         * Замечание: проверяем, что moveInput не равен нулю и совпадает со знаком текущего направления (facingRight).
         */
        if (collisionController.IsTouchingWall &&
            !collisionController.IsGrounded &&
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached > wallDetachCooldown)
        {
            StartWallHang();
        }

        // Отмена цепления (скольжения) при нажатии S
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
        // Если персонаж двигается на земле – выполняется подкат.
        if (Input.GetKeyDown(KeyCode.LeftControl) && collisionController.IsGrounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        // Если персонаж стоит на месте и зажат LeftControl – приседание (аналог S)
        else if (Input.GetKey(KeyCode.LeftControl) && collisionController.IsGrounded && Mathf.Abs(moveInput) < 0.01f)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero;
            }
        }
        // Приседание также реагирует на S
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

    // Метод запуска цепления за стену 
    // Теперь активируется независимо от вертикальной скорости и сбрасывает счётчик прыжков (для двойного прыжка).
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero;    // Полная остановка
            jumpCount = 0;                // Сбрасываем прыжки для возможности двойного прыжка
            Invoke("BeginWallSlide", wallHangTime);
        }
    }

    // Через wallHangTime запускается скольжение по стене
    private void BeginWallSlide()
    {
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

    // Метод поворота персонажа (Flip)
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}