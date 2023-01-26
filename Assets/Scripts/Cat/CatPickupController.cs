using UnityEngine;
using UnityEngine.Tilemaps;

public class CatPickupController : MonoBehaviour
{
    [SerializeField] private float throwSpeed = 3;
    [SerializeField] private Rigidbody2D catRigidbody;
    [SerializeField] private Tilemap hell;
    [SerializeField] private GameObject girl;
    [SerializeField] private SoulsController soulsController;
    [SerializeField] private Animator animator;
    [SerializeField] private GameManager gameManager;
    private bool isFallToHellAnimationPlaying;
    [SerializeField] private AudioSource deathSound;
    [SerializeField] private AudioSource landSound;

    private void Update()
    {
        if (!isFallToHellAnimationPlaying && this.gameObject.layer == Layers.Cat && 
            hell.HasTile(hell.WorldToCell(transform.position)))
        {
            Land();
        }
        if (isFallToHellAnimationPlaying)
        {
            var animStateInfo = animator.GetCurrentAnimatorStateInfo(0);
            var animName = animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
            
            if (animName.Equals(AnimationNames.Death) && animStateInfo.normalizedTime > 1.0f)
            {
                animator.SetBool("CatSacrificed",false);
                isFallToHellAnimationPlaying = false;
                // gameManager.down();
                gameObject.transform.position = girl.transform.position;
                if (soulsController.IsDead())
                {
                    gameManager.SetFocusedCharacter(true);
                    gameManager.ApplyFocusToCharacters();
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
        catRigidbody.velocity = throwSpeed * throwVelocity;
    }

    public void Land()
    {
        catRigidbody.velocity = Vector2.zero;
        animator.SetBool("CatInAir",false);
        Vector3Int catPos = hell.WorldToCell(transform.position);
        gameObject.layer = Layers.Cat;
        if (hell.HasTile(catPos))
        {
            soulsController.DecreaseSoul();
            deathSound.Play();
            animator.SetBool("CatSacrificed", true);
            isFallToHellAnimationPlaying = true;
            // gameManager.up();
        }
        else
        {
            // Cat has landed on ground, so it will be the active character now.
            landSound.Play();
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
        catRigidbody.velocity = Vector2.zero;
        gameObject.layer = Layers.Cat;
    }

    public bool isImportantAnimationPlaying()
    {
        return isFallToHellAnimationPlaying;
    }
    
    public void ResetImportantAnimation()
    {
        isFallToHellAnimationPlaying = false;
    }
}