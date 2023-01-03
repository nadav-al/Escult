using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CatInteractController : MonoBehaviour
{
    [SerializeField] private KeyCode interactButton;
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

    public List<Vector3Int> GetBridgeList()
    {
        return catBridgePositions;
    }
    

    private void Start()
    {
        soulsCtrl = GetComponent<SoulsController>();
        movementCtrl = GetComponent<MovementController>();
        catBridgePositions = new List<Vector3Int>();
    }

    void Update()
    {
        if (!isFocused)
        {
            return;
        }
        catDirection = movementCtrl.faceDirection;
        if (Input.GetKeyDown(interactButton))
        {
            if (catFacingPit())
            {
                soulsCtrl.DecreaseSoul();
                hellMap.SetTile(catBridgePositions.Last(), null);
                groundMap.SetTile(catBridgePositions.Last(), bloodTile);
                
            }
            else if (alterNearby && !alterController.GirlUnderGates(girl.position))
            {
                alterController.Sacrifice();
                soulsCtrl.DecreaseSoul();
            }
        }
    }

    private bool catFacingPit()
    {
        Vector3Int catCellLookPos = groundMap.WorldToCell(transform.position);
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

        if (hellMap.HasTile(catCellLookPos))
        {
            catBridgePositions.Add(catCellLookPos);
            return true;
        }
        return false;
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
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag(Tags.Alter))
        {
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
    }
}
