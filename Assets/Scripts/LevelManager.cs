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
    [SerializeField] private GameObject cat;
    private CatInteractController catInteractController;
    
    
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
                    child.GetComponent<CatPickupController>().Land();
                    child.GetComponent<SoulsController>().ResetSouls();
                    child.gameObject.SetActive(true);
                    break;
                case Tags.Door:
                    IOpenable doorCtrl = child.GetComponent<DoorController>();
                    doorCtrl.SetOpen(isDoorOpen);
                    break;
                case Tags.Grid:
                    foreach (var cell in catInteractController.GetBridgeList())
                    {
                        hellMap.SetTile(cell, hellTile);
                        groundMap.SetTile(cell, null);
                    }
                    break;
            }
        }
    }
}
