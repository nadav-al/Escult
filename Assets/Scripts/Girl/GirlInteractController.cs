using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GirlInteractController : MonoBehaviour
{
    [SerializeField] private KeyCode interactButtonOpt1 = KeyCode.E;
    [SerializeField] private KeyCode interactButtonOpt2 = KeyCode.J;

    [SerializeField] private Animator girlAnimator;
    [SerializeField] private MovementController movementController;
    private GameObject potentialHeldItem;
    private CatPickupController catController;
    private bool catInArea;
    private bool holdsCat;
    private bool isFocused;
    private bool isThrownAnimationPlaying;
    private List<String> animNames;
    [SerializeField] private GameObject gameManagerObj;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AudioSource catPickUpSound;
    [SerializeField] private AudioSource catThrowSound;

    public void SetFocus(bool isFocused)
    {
        this.isFocused = isFocused;
    }
    public void SetHoldsCat(bool isHoldingCat)
    {
        girlAnimator.SetBool("GirlHoldsCat",isHoldingCat);
        holdsCat = isHoldingCat;
    }
    public bool GetHoldsCat()
    {
        return holdsCat;
    }

    // Start is called before the first frame update
    private void Awake()
    {
        animNames = new List<String>
        {
            AnimationNames.ThrowLeft,
            AnimationNames.ThrowRight,
            AnimationNames.ThrowUp,
            AnimationNames.ThrowDown
        };
    }

    void Start()
    {
        // movementController = GetComponentInParent<MovementController>();
        // gameManager = gameManagerObj.GetComponent<GameManager>();
    }


    // Update is called once per frame
    void Update()
    {
        if (isThrownAnimationPlaying)
        {
            var animStateInfo = girlAnimator.GetCurrentAnimatorStateInfo(0);
            var animName = girlAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
            if (animNames.Contains(animName) && animStateInfo.normalizedTime > 1.0f)
            {
                girlAnimator.SetBool("Throws",false);
                isThrownAnimationPlaying = false;
                gameManager.down();
                SetHoldsCat(false);
                catController.Throw(transform.position, movementController.faceDirection);
                catThrowSound.Play();
            }    
        }
        
        if (!isFocused)
        {
            return;
        }
        
        if ((Input.GetKeyDown(interactButtonOpt1) || Input.GetKeyDown(interactButtonOpt2)))
        {
            if (catInArea)
            {
                catController.Pick();
                catPickUpSound.Play();
                SetHoldsCat(true);
                catInArea = false;
            }
            else if (holdsCat)
            {
                girlAnimator.SetBool("Throws",true);
                isThrownAnimationPlaying = true;
                gameManager.up();
            }
        }

        
        
    }


    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Cat))
        {
            catController = col.gameObject.GetComponent<CatPickupController>();
            catInArea = true;
        }
    }


    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Cat))
        {
            catInArea = false;
        }
    }

    public bool isImportantAnimationPlaying()
    {
        return isThrownAnimationPlaying;
    }

    public void ResetImportantAnimation()
    {
        isThrownAnimationPlaying = false;
    }
}