using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CatPickupController : MonoBehaviour
{
    [SerializeField] private float throwSpeed = 3;
    private Rigidbody2D rigidbody;
    private FaceDirection catDirection;
    [SerializeField] private Tilemap hell;
    [SerializeField] private GameObject girl;
    private SoulsController soulsController;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject gameManagerObj;
    private GameManager gameManager;
    private bool isFallToHellAnimationPlaying;

    private void Start()
    {
        soulsController = GetComponent<SoulsController>();
        rigidbody = GetComponent<Rigidbody2D>();
        gameManager = gameManagerObj.GetComponent<GameManager>();
    }

    private void Update()
    {
        if (isFallToHellAnimationPlaying)
        {
            var animStateInfo = animator.GetCurrentAnimatorStateInfo(0);
            var animName = animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
            
            if (animName.Equals(AnimationNames.Death) && animStateInfo.normalizedTime > 1.0f)
            {
                Debug.Log("You are out of hell");
                animator.SetBool("CatSacrificed",false);
                isFallToHellAnimationPlaying = false;
                gameManager.down();
                gameObject.transform.position = girl.transform.position;
                if (soulsController.IsDead())
                {
                    gameObject.SetActive(false);
                }
            }    
        }

    }

    public void Pick()
    {
        gameObject.SetActive(false);
    }

    public void Throw(Vector3 playerLoc, FaceDirection faceDirection)
    {
        gameManager.setColorToCat(true);
        gameObject.SetActive(true);
        gameObject.layer = Layers.Air;
        animator.SetBool("CatInAir",true);
        animator.SetInteger("FaceDirection", (int)faceDirection);
        catDirection = faceDirection;
        // Vector3Int cellPosition = hell.WorldToCell(playerLoc);
        // Vector3 cellCenter = hell.GetCellCenterWorld(cellPosition); 
        // gameObject.transform.position = cellCenter;
        gameObject.transform.position = playerLoc;
        Vector2 throwVelocity = Vector2.zero;
        switch (faceDirection)
        {
            case FaceDirection.Left:
                throwVelocity = Vector2.left;
                break;
            case FaceDirection.Right:
                throwVelocity = Vector2.right;
                break;
            case FaceDirection.Up:
                throwVelocity = Vector2.up;
                break;
            case FaceDirection.Down:
                throwVelocity = Vector2.down;
                break;
        }
        rigidbody.velocity = throwSpeed * throwVelocity;
    }

    public void Land()
    {
        rigidbody.velocity = Vector2.zero;
        gameObject.layer = Layers.Cat;
        animator.SetBool("CatInAir",false);
        Vector3Int catPos = hell.WorldToCell(transform.position);
        if (hell.HasTile(catPos))
        {
            Debug.Log("You are in hell");
            soulsController.DecreaseSoul();
            animator.SetBool("CatSacrificed", true);
            isFallToHellAnimationPlaying = true;
            gameManager.up();
        }
        else
        {
            // Cat has landed on ground, so it will be the active character now.
            gameManager.SetFocusedCharacter(false);    
        }
        // In Throw, we set the color of cat to be active. So 
        gameManager.ApplyFocusToCharacters();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.layer == Layers.Wall && this.gameObject.layer == Layers.Air)
        {
            Land();            
        }
    }

    public void setHellmap(Tilemap hellMap)
    {
        this.hell = hellMap;
    }

    public void dropCat(Vector3 playerLoc)
    {
        animator.SetBool("CatInAir",false);
        gameObject.transform.position = playerLoc;
        gameObject.SetActive(true); 
        rigidbody.velocity = Vector2.zero;
        gameObject.layer = Layers.Cat;
    }

    public bool isImportantAnimationPlaying()
    {
        return isFallToHellAnimationPlaying;
    }
}