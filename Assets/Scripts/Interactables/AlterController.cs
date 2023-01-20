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
    [SerializeField] private GameObject gateOutlinePrefab;
    private List<GameObject> gatesOutlines;
    [SerializeField] private GameObject altarOutlinePrefab;
    private GameObject altarOutline;

    private void Start()
    {
        openables = new List<IOpenable>();
        gatesTilemaps = new List<Tilemap>();
        gatesOutlines = new List<GameObject>();
        foreach (var obj in connectedObjects)
        {
            openables.Add(obj.GetComponent<IOpenable>());
            // TODO - better way to check if object has a tilemap
            if(!obj.CompareTag(Tags.Door))
            {
                var currGateTilemap = obj.GetComponent<Tilemap>(); 
                gatesTilemaps.Add(currGateTilemap);
                foreach (var position in currGateTilemap.cellBounds.allPositionsWithin)
                {
                    if (!currGateTilemap.HasTile(position))
                    {
                        continue;
                    }

                    GameObject currOutline = Instantiate(gateOutlinePrefab);
                    currOutline.transform.position = currGateTilemap.GetCellCenterWorld(position);
                    currOutline.SetActive(false);
                    gatesOutlines.Add(currOutline);
                }
            }
        }

        altarOutline = Instantiate(altarOutlinePrefab);
        altarOutline.transform.position = transform.position;
        altarOutline.SetActive(false);
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
        // foreach (var openable in openables)
        // {
        //     openable.ShowOutline(displayMode);
        // }
        foreach (var gOutline in gatesOutlines)
        {
            gOutline.SetActive(displayMode);
        }
    }
}