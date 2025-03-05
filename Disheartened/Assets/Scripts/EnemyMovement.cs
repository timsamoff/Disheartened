using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private float minJumpTiles = 1f; // Min tile height to attempt to jump
    [SerializeField] private float maxJumpTiles = 3f; // Max tile height to attempt to jump
    [SerializeField] private float minJumpHeight = 5f; // Minimum jump height
    [SerializeField] private float maxJumpHeight = 8f; // Maximum jump height
    [SerializeField] private float maxGapJumpDistance = 4f; // Max tiles the enemy can jump across

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
            ChaseTrail();
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
            // **Ensure forward movement mid-air**
            rb.linearVelocity = new Vector2(direction * movementData.runMaxSpeed, rb.linearVelocity.y);
        }
        else if (!isChasing)
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (isFlipping) return; // Don't move while flipping

        bool atWall = IsAtWall();
        bool atEdge = IsAtEdge();
        bool narrowSpace = IsInNarrowSpace();

        float targetSpeed = movementData.runMaxSpeed / 2; // Default patrol speed

        // **Start slowing down when approaching a wall**
        if (IsNearEdge() && !isChasing)
        {
            targetSpeed *= slowdownFactor; // Reduce speed smoothly before stopping
        }

        // **Ensure enemy keeps moving if it's in a narrow space**
        if (narrowSpace)
        {
            Debug.Log("Enemy is in a narrow space, continuing movement.");
            rb.linearVelocity = new Vector2(direction * movementData.runMaxSpeed, rb.linearVelocity.y);
            return;
        }

        // **Flip direction if at a wall or edge, using cooldown timers**
        if ((atWall || atEdge) && IsGrounded() && Time.time > lastFlipTime + flipCooldown)
        {
            StartCoroutine(FlipWithPause());
            lastFlipTime = Time.time;

            // **Reset speed so it smoothly eases into movement**
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        // **Ease into slowing down near obstacles**
        float newSpeed = Mathf.Lerp(rb.linearVelocity.x, direction * targetSpeed, Time.deltaTime * easingSpeed);
        rb.linearVelocity = new Vector2(newSpeed, rb.linearVelocity.y);
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

    private void ChaseTrail()
{
    if (isFlipping || isJumping) return;  // Avoid jumping or flipping when these actions are in progress
    if (!IsGrounded()) return;  // Don't jump if the enemy is not grounded

    // Chase the player while checking the jump condition
    Vector3 targetPosition = player.transform.position; // Chase the player directly
    float heightDifference = targetPosition.y - transform.position.y;
    float horizontalDistance = Mathf.Abs(targetPosition.x - transform.position.x);

    // **Check if the enemy should jump**
    if (IsGrounded() && ShouldJump()) 
    {
        Jump(); // Jump if at a wall or edge (based on the ShouldJump() logic)
    }

    // **Move toward the player**
    direction = targetPosition.x > transform.position.x ? 1 : -1;
    transform.localScale = new Vector3(direction, 1, 1);

    // Move horizontally toward the player
    float targetSpeed = movementData.runMaxSpeed;
    rb.linearVelocity = new Vector2(direction * targetSpeed, rb.linearVelocity.y);
}

    private Vector3? GetBestTrailNode()
    {
        if (PlayerTrail.instance == null) return null;

        List<Vector3> nodes = PlayerTrail.instance.GetTrailNodes();
        if (nodes.Count == 0) return null;

        Vector3 bestNode = Vector3.zero;
        float bestDistance = float.MaxValue;

        foreach (Vector3 node in nodes)
        {
            float enemyToNodeDist = Vector2.Distance(transform.position, node);

            // Ensure the node is ahead of the enemy in its current direction
            if ((direction == 1 && node.x < transform.position.x) ||
                (direction == -1 && node.x > transform.position.x))
            {
                continue; // Ignore nodes behind the enemy
            }

            // Pick the closest valid node
            if (enemyToNodeDist < bestDistance)
            {
                bestDistance = enemyToNodeDist;
                bestNode = node;
            }
        }

        return bestDistance < float.MaxValue ? bestNode : (Vector3?)null;
    }

    private bool ShouldJump()
    {
        return IsAtWall() || IsAtEdge();
    }

    private void Jump()
    {
        if (!IsGrounded() || hasJumped) return;

        isJumping = true;
        hasJumped = true;

        // Use minJumpHeight and maxJumpHeight instead of tile-based height calculations
        float jumpForce = Random.Range(minJumpHeight, maxJumpHeight);
        float forwardForce = movementData.runMaxSpeed * 0.8f;

        // **Reset Y velocity before jumping to prevent compounding jumps**
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);

        StartCoroutine(ApplyJumpArc(forwardForce, jumpForce));
        StartCoroutine(ResetJumpAfterLanding()); // Reset so another jump can occur
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

            // **Ensure forward force applies correctly for both left and right movement**
            float currentForwardForce = Mathf.Lerp(0, forwardForce, Mathf.Sqrt(t)) * direction;

            rb.linearVelocity = new Vector2(currentForwardForce, currentJumpForce);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        isJumping = false;
    }

    private IEnumerator ResetJumpAfterLanding()
    {
        yield return new WaitUntil(() => IsGrounded());
        hasJumped = false; // Allow another jump
    }

    private bool IsInNarrowSpace()
    {
        Vector2 leftCheck = transform.position + Vector3.left * tileHeight;
        Vector2 rightCheck = transform.position + Vector3.right * tileHeight;

        bool leftGround = Physics2D.Raycast(leftCheck, Vector2.down, checkDistance, groundLayer);
        bool rightGround = Physics2D.Raycast(rightCheck, Vector2.down, checkDistance, groundLayer);

        Debug.DrawRay(leftCheck, Vector2.down * checkDistance, Color.yellow);
        Debug.DrawRay(rightCheck, Vector2.down * checkDistance, Color.yellow);

        // **If only one side has ground, it's too narrow**
        return leftGround != rightGround;
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