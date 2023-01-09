using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AlterController : MonoBehaviour
{
    [SerializeField] private List<GameObject> connectedObjects;
    private List<IOpenable> openables;
    private List<Tilemap> tilemaps;

    private void Start()
    {
        openables = new List<IOpenable>();
        tilemaps = new List<Tilemap>();
        foreach (var obj in connectedObjects)
        {
            openables.Add(obj.GetComponent<IOpenable>());
            // TODO - better way to check if object has a tilemap
            if(!obj.CompareTag(Tags.Door))
            {
                tilemaps.Add(obj.GetComponent<Tilemap>());    
            }
        }
    }


    public void Sacrifice()
    {
        foreach (var openable in openables)
        {
            openable.SwapOpenState();
            // TODO - Remove DEBUG.LOG
            // Debug.Log(openable.getName() + "Status is: " + openable.GetOpenStatus());
        }
    }


    public bool GirlUnderGates(Vector3 girlPosition)
    {
        foreach (var tilemap in tilemaps)
        {
            if (tilemap.HasTile(tilemap.WorldToCell(girlPosition)))
            {
                return true;
            }
        }
        return false;
    }
}