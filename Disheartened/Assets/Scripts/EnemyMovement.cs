using UnityEngine;
using System.Collections;

public class EnemyMovement : MonoBehaviour
{
    [Header("Enemy Settings")]
    public Rigidbody2D rb;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Player Detection")]
    [SerializeField] private Transform player;
    [SerializeField] public PlayerData movementData;
    [SerializeField] private float detectionRangeX = 5f; // Horizontal detection range
    [SerializeField] private float detectionRangeY = 2.5f; // Vertical detection range
    [SerializeField] private float maxChaseRange = 10f; // Increases range when chasing
    [SerializeField] private float stopChasingTimer = 3f; // Time before stopping chase
    [SerializeField] private float checkDistance = 0.2f;
    [SerializeField] private float tileHeight = 0.5f;

    [Header("Movement Settings")]
    [SerializeField] private float slowdownFactor = 0.5f;
    [SerializeField] private float slowdownDistance = 0.6f;
    [SerializeField] private float flipPauseTime = 0.5f;
    [SerializeField] private float flipCooldown = 0.2f;
    private float lastFlipTime = 0f;
    [SerializeField] private float easingSpeed = 3f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;

    [Header("Jump Settings")]
    [SerializeField] private float maxJumpCheckDistance = 1.5f; // Max distance to check for jumpable tiles
    [SerializeField] private float minJumpHeight = 5f; // Minimum jump height
    [SerializeField] private float maxJumpHeight = 8f; // Maximum jump height
    [SerializeField] private float maxGapJumpDistance = 4f; // Max tiles the enemy can jump across

    private bool insideWallNull = false; // Tracks if the enemy is inside a WallNull
    private bool jumpAttempted = false; // Prevents repeated jumps


    [Header("Testing")]
    [SerializeField] private bool showGizmos = true;

    private bool hasJumped = false; // Tracks if the enemy has already jumped once

    private SpriteRenderer spriteRend;
    private EnemyAnimator anim;
    private int direction;
    private bool isChasing = false;
    private bool isFlipping = false;
    private bool isJumping = false;
    private float lastSeenTime;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend == null)
        {
            Debug.LogError("EnemyMovement: No SpriteRenderer found on " + gameObject.name);
        }

        anim = GetComponent<EnemyAnimator>();
        if (anim == null)
        {
            Debug.LogError("EnemyMovement: No EnemyAnimator found on " + gameObject.name);
        }

        // Start facing a random direction
        direction = Random.value < 0.5f ? -1 : 1;
        transform.localScale = new Vector3(direction, 1, 1);
    }

    void Update()
    {
        if (!IsGrounded()) return; // No logic applies while airborne

        if (PlayerInSight()) // If player is in range, start chasing
        {
            if (!isChasing) // Start chase mode if not already chasing
            {
                isChasing = true;
                Debug.Log("Enemy spotted the player! Starting chase.");
            }
            lastSeenTime = Time.time; // Reset the timer when the player is in range
        }
        else if (isChasing && Time.time > lastSeenTime + stopChasingTimer) // Player out of range for too long
        {
            isChasing = false;
            Debug.Log("Enemy lost the player. Returning to patrol.");
        }

        if (isChasing)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    private void FixedUpdate()
    {
        if (isJumping)
        {
            // **Ensure the enemy continues moving forward mid-air**
            rb.linearVelocity = new Vector2(direction * movementData.runMaxSpeed, rb.linearVelocity.y);
        }
    }


    private bool PlayerInSight()
    {
        if (player == null) return false;

        // Use separate X and Y detection ranges in patrol mode, full spherical range in chase mode
        if (!isChasing)
        {
            float distanceX = Mathf.Abs(player.position.x - transform.position.x);
            float distanceY = Mathf.Abs(player.position.y - transform.position.y);

            if (distanceX <= detectionRangeX && distanceY <= detectionRangeY)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, (player.position - transform.position).normalized, detectionRangeX, obstacleLayer);
                return hit.collider == null || hit.collider.CompareTag("Player");
            }
        }
        else
        {
            float distance = Vector2.Distance(transform.position, player.position); // Use full spherical range while chasing
            if (distance <= maxChaseRange)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, (player.position - transform.position).normalized, maxChaseRange, obstacleLayer);
                return hit.collider == null || hit.collider.CompareTag("Player");
            }
        }

        return false;
    }

    private void ChasePlayer()
    {
        if (isFlipping || isJumping) return;
        if (!IsGrounded()) return;

        float heightDifference = player.position.y - transform.position.y;
        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);

        // **Stop moving if the player is directly above**
        if (horizontalDistance < 0.5f && heightDifference > tileHeight * 1.5f)
        {
            rb.linearVelocity = Vector2.zero;
            Debug.Log("Enemy is waiting because the player is directly above.");
            return;
        }

        // Normal chase behavior
        direction = player.position.x > transform.position.x ? 1 : -1;
        transform.localScale = new Vector3(direction, 1, 1);

        rb.linearVelocity = new Vector2(direction * movementData.runMaxSpeed, rb.linearVelocity.y);

        // **Only jump if an obstacle is detected ahead**
        if (IsGrounded() && ShouldJump())
        {
            Jump();
        }
    }

    private void Jump()
    {
        if (!IsGrounded() || hasJumped) return;

        isJumping = true;
        hasJumped = true;

        float jumpForce = Random.Range(tileHeight * minJumpHeight, tileHeight * maxJumpHeight); // Jump between min and max tiles
        float forwardForce = movementData.runMaxSpeed * 0.8f;

        // **Reset Y velocity before jumping to prevent compounding jumps**
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);

        // **Start jump arc coroutine**
        StartCoroutine(ApplyJumpArc(forwardForce, jumpForce));
    }

    

    private void Patrol()
    {
        if (isFlipping) return;

        bool atWall = IsAtWall();
        bool atEdge = IsAtEdge();
        bool narrowSpace = IsInNarrowSpace();

        float targetSpeed = movementData.runMaxSpeed / 2;

        if (IsNearEdge() && !isChasing)
        {
            targetSpeed *= slowdownFactor; // Slow down near edges
        }

        // **If stuck in a narrow space, keep moving in the same direction**
        if (narrowSpace)
        {
            Debug.Log("Enemy is in a narrow space, continuing movement.");
            rb.linearVelocity = new Vector2(direction * targetSpeed, rb.linearVelocity.y);
            return;
        }

        if ((atWall || atEdge) && IsGrounded() && Time.time > lastFlipTime + flipCooldown)
        {
            StartCoroutine(FlipWithPause());
            lastFlipTime = Time.time;
        }

        float newSpeed = Mathf.Lerp(rb.linearVelocity.x, direction * targetSpeed, Time.deltaTime * easingSpeed);
        rb.linearVelocity = new Vector2(newSpeed, rb.linearVelocity.y);
    }

    private bool IsInNarrowSpace()
    {
        Vector2 leftCheck = transform.position + Vector3.left * 1f;
        Vector2 rightCheck = transform.position + Vector3.right * 1f;

        bool leftWall = Physics2D.Raycast(leftCheck, Vector2.left, 1f, groundLayer);
        bool rightWall = Physics2D.Raycast(rightCheck, Vector2.right, 1f, groundLayer);

        Debug.DrawRay(leftCheck, Vector2.left * 1f, Color.yellow);
        Debug.DrawRay(rightCheck, Vector2.right * 1f, Color.yellow);

        // **If both left and right walls exist within 2 tiles, it's a narrow space**
        return leftWall && rightWall;
    }

    private bool ShouldJump()
    {
        float maxJumpTile = tileHeight * 3f; // Jump up to 3 tiles high

        Vector2 forwardCheckPos = transform.position + new Vector3(direction * 0.6f, 0, 0);
        Vector2 upCheckPos = transform.position + new Vector3(direction * 0.6f, maxJumpTile, 0); // Check up to 3 tiles high

        bool isObstacleAhead = Physics2D.Raycast(forwardCheckPos, Vector2.right * direction, 0.6f, groundLayer);
        bool isSpaceAbove = !Physics2D.Raycast(upCheckPos, Vector2.up, maxJumpTile, groundLayer); // Ensure space to jump

        Debug.DrawRay(forwardCheckPos, Vector2.right * 0.6f * direction, Color.red);
        Debug.DrawRay(upCheckPos, Vector2.up * maxJumpTile, Color.green);

        // **Jump only if an obstacle is ahead and not too high**
        return (isObstacleAhead && isSpaceAbove && !TooHighToJump()) || insideWallNull;
    }

    // **Updated Method: Checks if an obstacle is taller than 3 tiles**
    private bool TooHighToJump()
    {
        float maxJumpTile = tileHeight * 3f;

        Vector2 checkPosition = transform.position + new Vector3(direction * 0.6f, maxJumpTile, 0);
        bool isTooHigh = Physics2D.Raycast(checkPosition, Vector2.up, maxJumpTile, groundLayer);

        Debug.DrawRay(checkPosition, Vector2.up * maxJumpTile, Color.blue);

        return isTooHigh;
    }

    private IEnumerator ApplyJumpArc(float forwardForce, float jumpForce)
    {
        float jumpDuration = 0.35f; // Controls the smoothness of the jump
        float elapsedTime = 0f;

        while (elapsedTime < jumpDuration)
        {
            float t = elapsedTime / jumpDuration;

            // **Vertical force starts strong, then gradually reduces (simulating gravity)**
            float currentJumpForce = jumpForce * (1 - t);

            // **Forward force starts low and gradually increases for a more natural arc**
            float currentForwardForce = forwardForce * Mathf.Sqrt(t);

            rb.linearVelocity = new Vector2(direction * currentForwardForce, currentJumpForce);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        isJumping = false;
    }

    private IEnumerator FlipWithPause()
    {
        isFlipping = true;
        direction *= -1;
        transform.localScale = new Vector3(direction, 1, 1);
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(flipPauseTime);

        isFlipping = false;
    }

    private bool IsGrounded() => Physics2D.Raycast(groundCheck.position, Vector2.down, checkDistance, groundLayer);

    private bool IsAtWall()
    {
        if (!IsGrounded()) return false;

        RaycastHit2D solidWallHit = Physics2D.Raycast(wallCheck.position, Vector2.right * direction, checkDistance, groundLayer);
        RaycastHit2D wallNullHit = Physics2D.Raycast(wallCheck.position, Vector2.right * direction, checkDistance);

        // Ensure the player is not blocking path
        if (solidWallHit.collider != null && solidWallHit.collider.CompareTag("Player"))
            return false;

        return solidWallHit.collider != null || (wallNullHit.collider != null && wallNullHit.collider.CompareTag("WallNull"));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("WallNull"))
        {
            insideWallNull = true;
            jumpAttempted = false; // Reset when entering a new WallNull
            Debug.Log("Enemy entered WallNull. Jump is now allowed.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("WallNull"))
        {
            insideWallNull = false;
            jumpAttempted = false; // Reset when exiting a WallNull
            Debug.Log("Enemy exited WallNull. Jump is now disabled.");
        }
    }

    private bool IsAtEdge() => !Physics2D.Raycast(groundCheck.position + (Vector3.right * direction * 0.3f), Vector2.down, checkDistance, groundLayer);

    private bool IsNearEdge() => !Physics2D.Raycast(groundCheck.position + (Vector3.right * direction * slowdownDistance * 1.2f), Vector2.down, checkDistance, groundLayer);

    void OnDrawGizmos()
    {
        // if (!Application.isPlaying) return;

        if (!showGizmos) return;

        // Tiles
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(tileHeight * 2, tileHeight * 2, 0));

        // Player movement
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, slowdownDistance);

        // Player Detection Ranges
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRangeX * 2, detectionRangeY * 2, 0));

        /*Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, slowdownDistance);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * detectionRangeY);*/

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxChaseRange);

        // Jump Parameters
        Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
        Gizmos.DrawWireSphere(transform.position, maxJumpCheckDistance);
        // Gizmos.DrawLine(transform.position, transform.position + Vector3.right * maxJumpCheckDistance * direction);

        Gizmos.color = new Color(1f, 0.4f, 0.7f); // Pink
        Gizmos.DrawWireSphere(transform.position, minJumpHeight);
        // Gizmos.DrawLine(transform.position, transform.position + Vector3.up * minJumpHeight);

        Gizmos.color = new Color(0.5f, 0f, 0.5f); // Purple
        Gizmos.DrawWireSphere(transform.position, maxJumpHeight);
        // Gizmos.DrawLine(transform.position, transform.position + Vector3.up * maxJumpHeight);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, maxGapJumpDistance);
        // Gizmos.DrawLine(transform.position, transform.position + Vector3.right * maxGapJumpDistance * direction);
    }
}