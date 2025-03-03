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
            isChasing = true;
            lastSeenTime = Time.time;
        }
        else if (isChasing && Time.time > lastSeenTime + stopChasingTimer) // Player left maxChaseRange
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

    private bool PlayerInSight()
    {
        if (player == null) return false;

        float distanceX = Mathf.Abs(player.position.x - transform.position.x);
        float distanceY = Mathf.Abs(player.position.y - transform.position.y);

        float maxDetectionX = isChasing ? maxChaseRange : detectionRangeX;

        if (distanceX <= maxDetectionX && distanceY <= detectionRangeY)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, (player.position - transform.position).normalized, maxDetectionX, obstacleLayer);

            return hit.collider == null || hit.collider.CompareTag("Player");
        }
        return false;
    }

    private void ChasePlayer()
    {
        if (isFlipping || isJumping) return;
        if (!IsGrounded()) return; // Prevent movement in air

        float heightDifference = player.position.y - transform.position.y;
        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);

        // **If the player is directly above within a small X-range, stay still**
        if (horizontalDistance < 0.5f && heightDifference > tileHeight * 1.5f)
        {
            rb.linearVelocity = Vector2.zero; // Stop movement
            Debug.Log("Enemy is waiting because the player is directly above.");
            return;
        }

        // **Otherwise, chase normally**
        direction = player.position.x > transform.position.x ? 1 : -1;
        transform.localScale = new Vector3(direction, 1, 1);

        // **Ensure enemy MOVES first before considering jumps**
        rb.linearVelocity = new Vector2(direction * movementData.runMaxSpeed, rb.linearVelocity.y);

        // **Check if movement is actually blocked**
        bool atWall = IsAtWall();
        bool atGap = ShouldJumpGap();
        bool canJump = CanJumpUp();
        bool playerBlocking = IsPlayerBlockingPath();

        Debug.Log($"Chasing Player | Wall: {atWall}, Gap: {atGap}, CanJump: {canJump}, PlayerBlocking: {playerBlocking}");

        // **1. If no obstacles, just move toward the player normally**
        if (!atWall && !atGap)
        {
            return; // Don't check for jumps unless movement is blocked
        }

        // **2. If at a wall, try jumping ONLY IF necessary**
        if (atWall && !playerBlocking && IsGrounded() && !hasJumped)
        {
            if (canJump)
            {
                JumpUpStairs();
                return;
            }
            else if (PlayerHasMovedBehind())
            {
                StartCoroutine(FlipWithPause()); // Flip instead of jumping
                return;
            }
        }

        // **3. If at a gap, jump across**
        if (atGap && !hasJumped)
        {
            JumpAcrossGap();
        }
    }

    private bool PlayerHasMovedBehind()
    {
        return (direction == 1 && player.position.x < transform.position.x) ||
               (direction == -1 && player.position.x > transform.position.x);
    }

    private void Patrol()
    {
        if (isFlipping) return; // If flipping, stop movement

        bool atWall = IsAtWall();
        bool atEdge = IsAtEdge();

        float targetSpeed = movementData.runMaxSpeed / 2;

        if (IsNearEdge() && !isChasing)
        {
            targetSpeed *= slowdownFactor; // Slow down near edges
        }

        if ((atWall || atEdge) && IsGrounded() && Time.time > lastFlipTime + flipCooldown)
        {
            StartCoroutine(FlipWithPause());
            lastFlipTime = Time.time;
        }

        float newSpeed = Mathf.Lerp(rb.linearVelocity.x, direction * targetSpeed, Time.deltaTime * easingSpeed);
        rb.linearVelocity = new Vector2(newSpeed, rb.linearVelocity.y);
    }

    private bool ShouldJumpGap()
    {
        if (!isChasing) return false;

        Vector2 checkPos = transform.position + new Vector3(direction * maxGapJumpDistance, 0, 0);
        bool groundAhead = Physics2D.Raycast(checkPos, Vector2.down, tileHeight * 1.5f, groundLayer);

        return !IsAtEdge() && groundAhead;
    }


    private void JumpAcrossGap()
    {
        isJumping = true;
        hasJumped = true;

        float jumpForce = Random.Range(minJumpHeight, maxJumpHeight);
        float forwardForce = movementData.runMaxSpeed * 0.75f; // Reduce gap jumping distance

        rb.linearVelocity = new Vector2(direction * forwardForce, jumpForce);
        anim.startedJumping = true;

        StartCoroutine(EndJump());
    }

    private bool CanJumpUp()
    {
        if (!isChasing || !insideWallNull || jumpAttempted) return false; // Only jump if inside a WallNull and hasn't jumped yet

        Vector2 checkPosition = transform.position + new Vector3(direction * maxJumpCheckDistance, 0, 0);

        // **Ensure there's no player blocking the jump**
        RaycastHit2D playerHit = Physics2D.Raycast(transform.position, Vector2.right * direction, checkDistance, LayerMask.GetMask("Player"));

        if (playerHit.collider != null)
        {
            Debug.Log("Player is blocking the jump! Aborting.");
            return false;
        }

        Debug.Log("WallNull detected and player is NOT blocking! Enemy will jump.");

        jumpAttempted = true; // Mark jump as attempted
        return true;
    }

    private IEnumerator EndJump()
    {
        yield return new WaitUntil(() => IsGrounded());

        isJumping = false;
        hasJumped = false;
        anim.justLanded = true;
    }

    private void JumpUpStairs()
    {
        if (!IsGrounded() || hasJumped) return;

        isJumping = true;
        hasJumped = true;

        float jumpForce = Random.Range(minJumpHeight, maxJumpHeight);
        float forwardForce = movementData.runMaxSpeed; // * 0.5f; // Reduce horizontal movement

        anim.startedJumping = true;
        StartCoroutine(ApplyJumpArc(forwardForce, jumpForce));
    }

    private IEnumerator ApplyJumpArc(float forwardForce, float jumpForce)
    {
        float jumpDuration = 0.35f; // Controls the smoothness of the jump
        float elapsedTime = 0f;

        while (elapsedTime < jumpDuration)
        {
            float t = elapsedTime / jumpDuration;

            // Vertical force starts strong, then gradually reduces (simulating gravity)
            float currentJumpForce = jumpForce * (1 - t);

            // Forward force starts low and gradually increases for a more natural arc
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
        if (!IsGrounded()) return false; // Ignore walls while falling

        // Check for a solid wall on the Ground layer (for patrolling)
        RaycastHit2D solidWallHit = Physics2D.Raycast(wallCheck.position, Vector2.right * direction, checkDistance, groundLayer);

        // Check for a "WallNull" trigger collider (for jumping logic)
        RaycastHit2D wallNullHit = Physics2D.Raycast(wallCheck.position, Vector2.right * direction, checkDistance);

        bool detectedWall = solidWallHit.collider != null || (wallNullHit.collider != null && wallNullHit.collider.CompareTag("WallNull"));

        if (detectedWall)
        {
            Debug.Log("Wall detected: " + (solidWallHit.collider != null ? "Ground" : "WallNull"));
        }

        return detectedWall;
    }

    private bool IsPlayerBlockingPath()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.right * direction, 1f, LayerMask.GetMask("Player"));
        return hit.collider != null;
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