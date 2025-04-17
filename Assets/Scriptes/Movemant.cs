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

    // Параметры цепляния за стену (Dead Cells-подобное поведение)
    public float wallHangTime = 0.5f;           // Время, в течение которого персонаж висит на стене
    public float wallSlideSpeed = 2f;           // Скорость скольжения по стене
    public float wallJumpForce = 10f;           // Сила прыжка от стены
    private bool isSlidingOnWall = false;
    private float wallDetachCooldown = 0.1f;
    private float timeSinceDetached;

    // Флаг направления (для поворота спрайта)
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

        // Движение персонажа (если он не цепляется за стену, не скользит и не приседает)
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Поворот персонажа
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // Прыжок (учитываем, что если персонаж касается земли, либо прыжков меньше max, либо цепляется за стену)
        if (Input.GetButtonDown("Jump") && (collisionController.IsGrounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

            // Если персонаж цепляется за стену, прыжок отталкивает его
            if (isSlidingOnWall)
            {
                rb.velocity = new Vector2(-Mathf.Sign(transform.localScale.x) * speed, wallJumpForce);
                isSlidingOnWall = false;
                timeSinceDetached = 0f;
            }

            jumpCount++;
        }

        // Обновляем счётчик прыжков при касании земли
        if (collisionController.IsGrounded)
        {
            jumpCount = 0;
            isSlidingOnWall = false;
        }

        // Логика цепляния за стену:
        if (collisionController.IsTouchingWall && !collisionController.IsGrounded && timeSinceDetached > wallDetachCooldown)
        {
            StartWallHang();
        }

        // Отмена скольжения по стене (при нажатии S)
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // Рывок (используем LeftShift)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash(moveInput));
        }

        // Подкат (используем LeftControl, только если персонаж двигается и на земле)
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSliding && collisionController.IsGrounded && moveInput != 0)
        {
            StartCoroutine(Slide(moveInput));
        }

        // Приседание (при удержании S на земле)
        if (Input.GetKey(KeyCode.S) && collisionController.IsGrounded && !isSliding && !isSlidingOnWall)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero;
            }
        }
        else if (isCrouching)
        {
            isCrouching = false;
        }
    }

    // Методы цепляния за стену
    private void StartWallHang()
    {
        if (!isSlidingOnWall) // Чтобы цепляться не несколько раз подряд
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero;   // Полностью останавливаем движение
            Invoke("BeginWallSlide", wallHangTime);  // По истечении hangTime начнётся скольжение
        }
    }

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

        // Если персонаж стоит, рывок ориентируется по направлению взгляда
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