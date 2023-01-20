using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class AlterController : MonoBehaviour
{
    [SerializeField] private List<GameObject> connectedObjects;
    private List<IOpenable> openables;
    private List<Tilemap> gatesTilemaps;
    [SerializeField] private GameObject gateOutline;
    private List<GameObject> gatesOutlines;
    [SerializeField] private GameObject altarOutline;

    private void Start()
    {
        openables = new List<IOpenable>();
        gatesTilemaps = new List<Tilemap>();
        foreach (var obj in connectedObjects)
        {
            openables.Add(obj.GetComponent<IOpenable>());
            // TODO - better way to check if object has a tilemap
            if(!obj.CompareTag(Tags.Door))
            {
                gatesTilemaps.Add(obj.GetComponent<Tilemap>());
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
        foreach (var gate in gatesTilemaps)
        {
            if (gate.HasTile(gate.WorldToCell(girlPosition)))
            {
                return true;
            }
        }
        return false;
    }

    public void ShowOutlines(bool displayMode)
    {
        altarOutline.SetActive(displayMode);
        foreach (var openable in openables)
        {
            openable.ShowOutline(displayMode);
        }
    }
}