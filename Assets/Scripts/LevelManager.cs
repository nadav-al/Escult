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
    private Animator catAnimator; 
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
        cat.SetActive(isActive);
        girl.SetActive(isActive);
    }

    public void ResetLevel()
    {
        // Reset Cat
        soulsController.ResetSouls();
        if (isCatInLevel)
        {
            catInteractController.ResetBloodBridgeOutline();
            cat.transform.position = catPos;
            catPickupController.dropCat(catPos);
            cat.SetActive(true);    
        }
        

        // Reset Girl
        girl.transform.position = girlPos;
        girl.SetActive(true); 
        girlInteractController.SetHoldsCat(false);

        GateContoller gateCtrl;
        // Reset Gates
        foreach (var gate in gates)
        {
            gate.SetActive(true);
            gateCtrl = gate.GetComponent<GateContoller>(); 
            // gateCtrl.EstablishOutlines();
            gateCtrl.ResetGate();
        }

        // Reset Tilemaps
        List<Vector3Int> bridgeLocations = catInteractController.GetBridgeList();
        List<TileBase> hellOrigTiles = catInteractController.GetOriginalHellTileList();
        for (int i = 0; i < bridgeLocations.Count; ++i)
        {
            hellMap.SetTile(bridgeLocations[i], hellOrigTiles[i]);
            groundMap.SetTile(bridgeLocations[i], null);
        }
        
        // Reset Doors
        foreach (var door in doors)
        {
            IOpenable doorCtrl = door.GetComponent<DoorController>();
            doorCtrl.SetOpen(isDoorOpen);
        }
        
        catAnimator.Play(AnimationNames.ReviveState);
    }

    private void ResetComponents()
    {
        catPickupController = cat.GetComponent<CatPickupController>();
        catInteractController = cat.GetComponent<CatInteractController>();
        soulsController = cat.GetComponent<SoulsController>();
        catAnimator = cat.GetComponent<Animator>();
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
        cat.SetActive(true);
        girl.SetActive(true);
        ResetComponents();
        SetCatTilemaps();
        ResetLevel();
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