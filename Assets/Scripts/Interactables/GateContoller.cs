using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateContoller : MonoBehaviour, IOpenable
{
    [SerializeField] private bool INITIAL_GATE_STATUS; 
    private bool gateStatus;

    // Start is called before the first frame update
    void Start()
    {
        // ResetGate();
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

    public void ResetGate()
    {
        SetOpen(INITIAL_GATE_STATUS);
    }

    public void SwapOpenState()
    {
        SetOpen(!gateStatus);
        // gateStatus = !gateStatus;
        // gameObject.SetActive(gateStatus);
    }

    public string getName()
    {
        return gameObject.name;
    }

    public void ShowOutline(bool displayMode)
    {
        throw new System.NotImplementedException();
    }
}
