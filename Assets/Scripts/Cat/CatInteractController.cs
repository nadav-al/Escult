using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CatInteractController : MonoBehaviour
{
    [SerializeField] private KeyCode interactButtonOpt1 = KeyCode.E;
    [SerializeField] private KeyCode interactButtonOpt2 = KeyCode.J;
    private bool isFocused = false;
    private bool alterNearby;
    private AlterController alterController;
    private SoulsController soulsCtrl;
    [SerializeField] private Transform girl;
    private FaceDirection catDirection;
    private MovementController movementCtrl;
    [SerializeField] private Tilemap hellMap;
    [SerializeField] private Tilemap groundMap;
    [SerializeField] private TileBase bloodTile;
    private List<Vector3Int> catBridgePositions;
    private List<TileBase> hellOriginalTiles;
    [SerializeField] private GameObject gameManagerObj;
    private GameManager gameManager;
    private List<GameObject> gates;
    [SerializeField] private Animator animator;
    private bool isSacrificeAnimationPlaying;
    private List<String> animNames;
    private bool isDeathAfterBridgeAnimationPlaying;
    private CircleCollider2D circleCollider;
    private Vector3Int catCellLookPos;

    public List<Vector3Int> GetBridgeList()
    {
        return catBridgePositions;
    }

    public List<TileBase> GetOriginalHellTileList()
    {
        return hellOriginalTiles;
    }
    
    

    private void Start()
    {
        gates = new List<GameObject>();
        gameManager = gameManagerObj.GetComponent<GameManager>();
        soulsCtrl = GetComponent<SoulsController>();
        movementCtrl = GetComponent<MovementController>();
        catBridgePositions = new List<Vector3Int>();
        hellOriginalTiles = new List<TileBase>();
        animNames = new List<String>
        {
            AnimationNames.Death, AnimationNames.Revive
        };
        circleCollider = GetComponent<CircleCollider2D>();
    }

    void Update()
    {
        animator.SetInteger("CatSouls", soulsCtrl.getSouls());
        
        if (isSacrificeAnimationPlaying)
        {
            var animStateInfo1 = animator.GetCurrentAnimatorStateInfo(0);
            var animName1 = animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;

            if (animNames.Contains(animName1) && animStateInfo1.normalizedTime > 1.0f)
            {
                isSacrificeAnimationPlaying = false;
                gameManager.down();
                animator.SetBool("CatSacrificed", false);
                if (soulsCtrl.IsDead())
                {
                    gameObject.SetActive(false);
                }
            }
        }    
        
        if (isDeathAfterBridgeAnimationPlaying)
        {
            var animStateInfo2 = animator.GetCurrentAnimatorStateInfo(0);
            var animName2 = animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
        
            if (animName2.Equals(AnimationNames.Death) && animStateInfo2.normalizedTime > 1.0f)
            {
                Debug.Log("You are out of hell");
                isDeathAfterBridgeAnimationPlaying = false;
                gameManager.down();
                gameObject.SetActive(false);
            }    
        }

        if (!isFocused || gameManager.isImportantAnimationsPlaying())
        {
            return;
        }
        catDirection = movementCtrl.faceDirection;
        
        
        //TODO Changes 18.1: order of alter and pit are switched.
        if (gameObject.layer != Layers.Air && (Input.GetKeyDown(interactButtonOpt1) || Input.GetKeyDown(interactButtonOpt2)))
        {
            if (alterNearby && !alterController.GirlUnderGates(girl.position))
            {
                animator.SetBool("CatSacrificed", true);
                isSacrificeAnimationPlaying = true;
                gameManager.up();
                alterController.Sacrifice();
                soulsCtrl.DecreaseSoul();
            }
            else if (catFacingPit())
            {
                addLocationToBridges();
                soulsCtrl.DecreaseSoul();
                hellMap.SetTile(catBridgePositions.Last(), null);
                groundMap.SetTile(catBridgePositions.Last(), bloodTile);
                if (soulsCtrl.IsDead())
                {
                    animator.SetInteger("CatSouls", 0);
                    isDeathAfterBridgeAnimationPlaying = true;
                    gameManager.up();
                }
            } 
        }

        
    }


    // This function also adds the gate to the list and as such, we must make sure here that we can build
    // The gate.
    private bool catFacingPit()
    {
        catCellLookPos = groundMap.WorldToCell(circleCollider.bounds.center);
        switch (catDirection)
        {
            case FaceDirection.Up:
                catCellLookPos += Vector3Int.up;
                break;
            case FaceDirection.Right:
                catCellLookPos += Vector3Int.right;
                break;
            case FaceDirection.Down:
                catCellLookPos += Vector3Int.down;
                break;
            case FaceDirection.Left:
                catCellLookPos += Vector3Int.left;
                break;
        }

        if (hellMap.HasTile(catCellLookPos) && (!gameManager.getLevel().hasGates(catCellLookPos)))
        {
            return true;
        }
        return false;
    }
    
    
    private void addLocationToBridges()
    {
        catBridgePositions.Add(catCellLookPos);
        hellOriginalTiles.Add(hellMap.GetTile(catCellLookPos));
    }

    public void SetFocus(bool isFocused)
    {
        this.isFocused = isFocused;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag(Tags.Alter))
        {
            alterNearby = true;
            alterController = col.gameObject.GetComponent<AlterController>();
            // alterController.ShowOutlines(true);
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag(Tags.Alter))
        {
            // alterController.ShowOutlines(false);
            alterNearby = false;
            alterController = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Steppable))
        {
            col.gameObject.GetComponent<IStepable>().StepOn();
        }
    }


    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Steppable))
        {
            col.gameObject.GetComponent<IStepable>().StepOff();
        }
    }

    public void setHellmap(Tilemap tilemap)
    {
        hellMap = tilemap;
    }
    
    public void setGroundmap(Tilemap tilemap)
    {
        groundMap = tilemap;
    }

    public void ResetBridgeList()
    {
        catBridgePositions.Clear();
        hellOriginalTiles.Clear();
    }

    public void setGates(List<GameObject> gatesList)
    {
        gates = gatesList;
    }

    public void resetGates()
    {
        gates.Clear();
    }

    public bool isImportantAnimationPlaying()
    {
        return isSacrificeAnimationPlaying || isDeathAfterBridgeAnimationPlaying;
    }
}
