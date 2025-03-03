using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    private EnemyMovement mov; // Change from PlayerMovement to EnemyMovement
    private Animator anim;
    private SpriteRenderer spriteRend;

    [Header("Movement Tilt")]
    [SerializeField] private float maxTilt;
    [SerializeField][Range(0, 1)] private float tiltSpeed;

    [Header("Particle FX")]
    [SerializeField] private GameObject jumpFX;
    [SerializeField] private GameObject landFX;
    private ParticleSystem _jumpParticle;
    private ParticleSystem _landParticle;

    public bool startedJumping { private get; set; }
    public bool justLanded { private get; set; }

    public float currentVelY;

    private void Start()
    {
        mov = GetComponent<EnemyMovement>(); // Get the EnemyMovement component
        if (mov == null)
        {
            Debug.LogError("EnemyAnimator: EnemyMovement script is missing on " + gameObject.name);
        }

        spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend == null)
        {
            Debug.LogError("EnemyAnimator: No SpriteRenderer found on " + gameObject.name);
        }

        anim = spriteRend != null ? spriteRend.GetComponent<Animator>() : null;
        if (anim == null)
        {
            Debug.LogError("EnemyAnimator: No Animator found on " + (spriteRend != null ? spriteRend.gameObject.name : "null SpriteRenderer"));
        }

        if (jumpFX != null)
        {
            _jumpParticle = jumpFX.GetComponent<ParticleSystem>();
        }
        if (landFX != null)
        {
            _landParticle = landFX.GetComponent<ParticleSystem>();
        }
    }

    private void LateUpdate()
    {
        if (mov == null || spriteRend == null || anim == null) return;

        #region Tilt
        float tiltProgress;
        int mult = -1;

        tiltProgress = Mathf.InverseLerp(-mov.movementData.runMaxSpeed, mov.movementData.runMaxSpeed, mov.rb.linearVelocity.x);
        mult = (mov.transform.localScale.x > 0) ? 1 : -1;

        float newRot = ((tiltProgress * maxTilt * 2) - maxTilt);
        float rot = Mathf.LerpAngle(spriteRend.transform.localRotation.eulerAngles.z * mult, newRot, tiltSpeed);
        spriteRend.transform.localRotation = Quaternion.Euler(0, 0, rot * mult);
        #endregion

        CheckAnimationState();

        if (_jumpParticle != null)
        {
            ParticleSystem.MainModule jumpPSettings = _jumpParticle.main;
        }
        if (_landParticle != null)
        {
            ParticleSystem.MainModule landPSettings = _landParticle.main;
        }
    }

    private void CheckAnimationState()
    {
        if (startedJumping)
        {
            anim.SetTrigger("Jump");
            GameObject obj = Instantiate(jumpFX, transform.position - (Vector3.up * transform.localScale.y / 2), Quaternion.Euler(-90, 0, 0));
            Destroy(obj, 1);
            startedJumping = false;
            return;
        }

        if (justLanded)
        {
            anim.SetTrigger("Land");
            GameObject obj = Instantiate(landFX, transform.position - (Vector3.up * transform.localScale.y / 1.5f), Quaternion.Euler(-90, 0, 0));
            Destroy(obj, 1);
            justLanded = false;
            return;
        }

        anim.SetFloat("Vel Y", mov.rb.linearVelocity.y);
    }
}