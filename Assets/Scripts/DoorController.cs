using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [SerializeField] private GameObject doorClosed;
    [SerializeField] private GameObject doorOpened;

    private bool openStatus;
    
    // Start is called before the first frame update
    void Start()
    {
        doorClosed.SetActive(true);
        doorOpened.SetActive(false);
    }

    public void setOpen(bool openStatus)
    {
        this.openStatus = openStatus;
        doorClosed.SetActive(!openStatus);
        doorOpened.SetActive(openStatus);
    }

    public bool getOpenStatus()
    {
        return openStatus;
    }
}
