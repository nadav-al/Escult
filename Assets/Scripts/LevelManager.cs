using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private GameObject girl;
    [SerializeField] private Vector3 girlPos;
    private GirlInteractController girlInteractController;
    [SerializeField] private GameObject cat;
    [SerializeField] private bool isCatInLevel = true;
    [SerializeField] private Vector3 catPos;
    private CatInteractController catInteractController;
    private CatPickupController catPickupController;
    [SerializeField] private TileBase hellTile;
    [SerializeField] private Tilemap groundMap;
    [SerializeField] private Tilemap hellMap;
    private SoulsController soulsController;
    [SerializeField] private List<GameObject> gates;
    [SerializeField] private List<GameObject> doors;
    [SerializeField] private bool isDoorOpen;


    // Start is called before the first frame update
    void Start()
    {
        catInteractController = cat.GetComponent<CatInteractController>();
        catPickupController = cat.GetComponent<CatPickupController>();
        soulsController = cat.GetComponent<SoulsController>();
        girlInteractController = girl.GetComponent<GirlInteractController>();
        if (!isCatInLevel)
        {
            cat.SetActive(false);
        }
    }
    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
        // ResetComponents();
    }

    public void ResetLevel()
    {
        // TODO - check if needed
        // // Reset Components
        // ResetComponents();

        // Reset Cat
        soulsController.ResetSouls();
        if (isCatInLevel)
        {
            cat.transform.position = catPos;
            catPickupController.dropCat(catPos);
            cat.SetActive(true);    
        }
        

        // Reset Girl
        girl.transform.position = girlPos;
        girl.SetActive(true); 
        girlInteractController.SetHoldsCat(false);

        // Reset Gates
        foreach (var gate in gates)
        {
            gate.GetComponent<GateContoller>().ResetGate();
        }

        // Reset Tilemaps
        foreach (var cell in catInteractController.GetBridgeList())
        {
            hellMap.SetTile(cell, hellTile);
            groundMap.SetTile(cell, null);
        }
        
        // Reset Doors
        foreach (var door in doors)
        {
            IOpenable doorCtrl = door.GetComponent<DoorController>();
            doorCtrl.SetOpen(isDoorOpen);
        }
    }

    private void ResetComponents()
    {
        catPickupController = cat.GetComponent<CatPickupController>();
        catInteractController = cat.GetComponent<CatInteractController>();
        soulsController = cat.GetComponent<SoulsController>();
        girlInteractController = girl.GetComponent<GirlInteractController>();
    }

    private void SetCatTilemaps()
    {
        catPickupController.setHellmap(hellMap);
        catInteractController.setHellmap(hellMap);
        catInteractController.setGroundmap(groundMap);
        catInteractController.setGates(gates);
    }

    public void StartNewLevel()
    {
        // Reset Components
        ResetComponents();
        SetCatTilemaps();
        ResetLevel();
        //
        // // Set up the cat
        // cat.transform.position = catPos;
        // cat.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        // cat.layer = Layers.Cat;
        // cat.GetComponent<SoulsController>().ResetSouls();
        // // Set up the cat needed tilemaps
        // catInteractController = cat.GetComponent<CatInteractController>();
        // catInteractController.setGroundmap(groundMap);
        // catInteractController.setHellmap(hellMap);
        // catInteractController.setGates(gates);
        // cat.GetComponent<CatPickupController>().setHellmap(hellMap);
        // cat.SetActive(true);
        //
        // // Set up the girl
        // girl.transform.position = girlPos;
        // girl.SetActive(true);
        // girl.GetComponent<GirlInteractController>().SetHoldsCat(false);
        //
        // // Set up the gates.
    }

    public bool hasGates(Vector3Int catCellLookPos)
    {
        foreach (var gate in gates)
        {
            if (gate.activeSelf && gate.GetComponent<Tilemap>().HasTile(catCellLookPos))
            {
                return true;
            }
        }

        return false;
    }

    public bool getCatInLevel()
    {
        return isCatInLevel;
    }
}
