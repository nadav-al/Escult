using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateContoller : MonoBehaviour, IOpenable
{
    [SerializeField] private bool gateStatus;

    // Start is called before the first frame update
    void Start()
    {
        SetOpen(gateStatus);
    }
    
    public void SetOpen(bool openStatus)
    {
        gateStatus = openStatus;
        gameObject.SetActive(openStatus);
    }
    
    public bool GetOpenStatus()
    {
        return gateStatus;
    }

    public void SwapOpenState()
    {
        gateStatus = !gateStatus;
        gameObject.SetActive(gateStatus);
    }
}
