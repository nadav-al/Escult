using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private Vector3 girlPos;
    [SerializeField] private Vector3 catPos;
    [SerializeField] private bool isDoorOpen;
    [SerializeField] private TileBase hellTile;
    [SerializeField] private Tilemap groundMap;
    [SerializeField] private Tilemap hellMap;
    [SerializeField] private GameObject girl;
    [SerializeField] private GameObject cat;
    private CatInteractController catInteractController;
    [SerializeField] private List<GameObject> gates;
    
    
    // Start is called before the first frame update
    void Start()
    {
        catInteractController = cat.GetComponent<CatInteractController>();
    }
    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    public void ResetLevel()
    {
        Debug.Log("Before");
        Debug.Log(girl.GetComponentInChildren<ColliderGirlInteractController>().gameManager);

        cat.transform.position = catPos;
        // TODO - MAYBE PROBLEM WITH THE FUNCTION
        cat.GetComponent<CatPickupController>().dropCat(catPos);
        // not using Land because of the ifClause inside the Land function
        // cat.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        // gameObject.layer = Layers.Ground;
        
        
        cat.GetComponent<SoulsController>().ResetSouls();
        catInteractController = cat.GetComponent<CatInteractController>();
        // TODO - instead of in reset level, maybe make a new method that is called once when switching level
        // No need to set the maps every time we press R, just need it everytime we swap levels.
        // Set the maps there.
        catInteractController.setHellmap(hellMap);
        catInteractController.setGroundmap(groundMap);
        catInteractController.setGates(gates);

        cat.GetComponent<CatPickupController>().setHellmap(hellMap);
        cat.SetActive(true);
        girl.transform.position = girlPos;
        girl.SetActive(true); 
        girl.GetComponent<GirlInteractController>().SetHoldsCat(false);

        foreach (var gate in gates)
        {
            gate.GetComponent<GateContoller>().ResetGate();
        }
        // children = GetComponentsInChildren<GameObject>();
        foreach (Transform child in transform)
        {
            switch (child.tag)
            {
                case Tags.Girl:
                    child.position = girlPos;
                    child.gameObject.SetActive(true);
                    child.GetComponent<GirlInteractController>().SetHoldsCat(false);
                    break;
                case Tags.Cat:
                    child.position = catPos;
                    child.GetComponent<CatPickupController>().dropCat(catPos);
                    child.GetComponent<SoulsController>().ResetSouls();
                    child.gameObject.SetActive(true);
                    break;
                case Tags.Door:
                    IOpenable doorCtrl = child.GetComponent<DoorController>();
                    doorCtrl.SetOpen(isDoorOpen);
                    break;
                case Tags.Grid:
                    // TODO - catInteractController is null after next level so i switched it with getComponent
                    foreach (var cell in catInteractController.GetBridgeList())
                    {
                        hellMap.SetTile(cell, hellTile);
                        groundMap.SetTile(cell, null);
                    }
                    break;
            }
        }
        Debug.Log("After");
        Debug.Log(girl.GetComponentInChildren<ColliderGirlInteractController>().gameManager);

    }

    public void StartNewLevel()
    {
        // Set up the cat
        cat.transform.position = catPos;
        cat.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        cat.layer = Layers.Cat;
        cat.GetComponent<SoulsController>().ResetSouls();
        // Set up the cat needed tilemaps
        catInteractController = cat.GetComponent<CatInteractController>();
        catInteractController.setGroundmap(groundMap);
        catInteractController.setHellmap(hellMap);
        catInteractController.setGates(gates);
        cat.GetComponent<CatPickupController>().setHellmap(hellMap);
        cat.SetActive(true);

        // Set up the girl
        girl.transform.position = girlPos;
        girl.SetActive(true);
        girl.GetComponent<GirlInteractController>().SetHoldsCat(false);
        
        // Set up the gates.
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
}
