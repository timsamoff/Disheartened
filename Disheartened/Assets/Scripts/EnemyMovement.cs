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

        Debug.Log($"Chasing Player | InsideWallNull: {insideWallNull}");

        // **If inside a WallNull, immediately attempt to jump**
        if (insideWallNull && IsGrounded() && !hasJumped)
        {
            if (CanJumpUp())
            {
                Debug.Log("Enemy is inside WallNull and will now jump.");
                JumpUpStairs();
                return;
            }
        }
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

    private bool CanJumpUp()
    {
        if (!insideWallNull) return false; // Only check WallNull
        if (jumpAttempted) return false;   // Prevent repeated jumps

        Debug.Log("Enemy is inside WallNull and will attempt to jump.");
        jumpAttempted = true;
        return true;
    }

    private IEnumerator EndJump()
    {
        yield return new WaitUntil(() => IsGrounded());
        isJumping = false;
        hasJumped = false;
        jumpAttempted = false; // Reset this so the enemy can jump again
        anim.justLanded = true;
        Debug.Log("Enemy landed. Jump reset.");
    }


    private void JumpUpStairs()
    {
        if (!IsGrounded() || hasJumped) return;

        isJumping = true;
        hasJumped = true;

        // Get enemy's height from its collider
        float enemyHeight = GetComponent<Collider2D>().bounds.size.y;

        // Randomized jump force between 1x and 3x the enemy's height
        float jumpForce = Random.Range(enemyHeight * minJumpHeight, enemyHeight * maxJumpHeight);

        float forwardForce = movementData.runMaxSpeed * 0.8f;

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
        if (!IsGrounded()) return false;

        RaycastHit2D solidWallHit = Physics2D.Raycast(wallCheck.position, Vector2.right * direction, checkDistance, groundLayer);
        RaycastHit2D wallNullHit = Physics2D.Raycast(wallCheck.position, Vector2.right * direction, checkDistance);

        // Ensure the player is not blocking path
        if (solidWallHit.collider != null && solidWallHit.collider.CompareTag("Player"))
            return false;

        return solidWallHit.collider != null || (wallNullHit.collider != null && wallNullHit.collider.CompareTag("WallNull"));
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