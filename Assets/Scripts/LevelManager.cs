using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private GameObject girl;
    [SerializeField] public Vector3 girlPos;
    private GirlInteractController girlInteractController;
    [SerializeField] private GameObject cat;
    [SerializeField] private bool isCatInLevel = true;
    [SerializeField] private Vector3 catPos;
    private Animator catAnimator; 
    private Animator girlAnimator;
    [SerializeField] private CatInteractController catInteractController;
    [SerializeField] private CatPickupController catPickupController;
    [SerializeField] private TileBase hellTile;
    [SerializeField] private Tilemap groundMap;
    [SerializeField] private Tilemap hellMap;
    private SoulsController soulsController;
    [SerializeField] private List<GameObject> gates;
    [SerializeField] private List<GameObject> doors;
    [SerializeField] private List<GameObject> altars;
    [SerializeField] private bool isDoorOpen;
    private GraphicCatSoulsController catGraphicSoulsController;


    // Start is called before the first frame update
    void Awake()
    {
        catInteractController = cat.GetComponent<CatInteractController>();
        catPickupController = cat.GetComponent<CatPickupController>();
        soulsController = cat.GetComponent<SoulsController>();
        girlInteractController = girl.GetComponent<GirlInteractController>();
        catAnimator = cat.GetComponent<Animator>();
        girlAnimator = girl.GetComponent<Animator>();
        catGraphicSoulsController = cat.GetComponent<GraphicCatSoulsController>();
        if (!isCatInLevel)
        {
            cat.SetActive(false);
        }
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
        ResetComponents();
        
    }

    public void ResetLevel()
    {
        // Reset Cat
        soulsController.ResetSouls();
        if (isCatInLevel)
        {
            catInteractController.ResetBloodBridgeOutline();
            catInteractController.getRidOfMovingBridges();
            cat.transform.position = catPos;
            catPickupController.dropCat(catPos);
            cat.SetActive(true);
            catAnimator.Rebind();
            catGraphicSoulsController.ShowHealthBar(true);
        }
        else
        {
            cat.SetActive(false);
            catGraphicSoulsController.ShowHealthBar(false);
        }
        

        // Reset Girl
        girl.transform.position = girlPos;
        girl.SetActive(true); 
        girlInteractController.SetHoldsCat(false);
        girlAnimator.Rebind();

        GateContoller gateCtrl;
        // Reset Gates
        foreach (var gate in gates)
        {
            gate.SetActive(true);
            gateCtrl = gate.GetComponent<GateContoller>(); 
            gateCtrl.ResetGate();
        }
        
        // Reset Tilemaps
        resetBloodTiles();
        
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
        girlAnimator = girl.GetComponent<Animator>();
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

    public void resetBloodTiles()
    {
        List<Vector3Int> bridgeLocations = catInteractController.GetBridgeList();
        List<TileBase> hellOrigTiles = catInteractController.GetOriginalHellTileList();
        for (int i = 0; i < bridgeLocations.Count; ++i)
        {
            hellMap.SetTile(bridgeLocations[i], hellOrigTiles[i]);
            groundMap.SetTile(bridgeLocations[i], null);
        }
    }

    public void DestroyLevelOutlines()
    {
        foreach (var altar in altars)
        {
            altar.GetComponent<AlterController>().DestroyOutlines();
        }
    }
}