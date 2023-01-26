using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderGirlInteractController : MonoBehaviour
{
    [SerializeField] private GameObject gameManagerObj;
    [SerializeField] private Animator catAnimator;
    [SerializeField] public GameManager gameManager;
    [SerializeField] public GirlInteractController girlInteractController;
    [SerializeField] public GameObject cat;
    [SerializeField] private AudioSource deathCatSound;
    private bool isEndOfLevelAnimationPlaying;

    private void Start()
    {
    }


    private void Update()
    {
        if (isEndOfLevelAnimationPlaying)
        {
            var animStateInfo2 = catAnimator.GetCurrentAnimatorStateInfo(0);
            var animName2 = catAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
        
            if (animName2.Equals(AnimationNames.CatLeavesLevel) && animStateInfo2.normalizedTime > 1.0f)
            {
                isEndOfLevelAnimationPlaying = false;
                gameManager.down();
                gameManager.NextLevel();
            }    
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.CompareTag(Tags.Door))
        {
            IOpenable door = col.gameObject.GetComponent<DoorController>();
            if (door.GetOpenStatus())
            {
                if (!gameManager.getLevel().getCatInLevel() || gameManager.isCatDead() || 
                    girlInteractController.GetHoldsCat() || cat.layer == Layers.Air || 
                    gameManager.isImportantAnimationsPlaying())
                {
                    if (deathCatSound.isPlaying)
                    {
                        deathCatSound.Stop();
                    }
                    gameManager.NextLevel();
                    
                }
                else
                {
                    catAnimator.SetTrigger("LevelEnded");
                    isEndOfLevelAnimationPlaying = true;
                    gameManager.up();
                }
                
            }
        }
    }

    public bool isImportantAnimationPlaying()
    {
        return isEndOfLevelAnimationPlaying;
    }
    
    public void ResetImportantAnimation()
    {
        isEndOfLevelAnimationPlaying = false;
    }
}
