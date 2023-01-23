using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour, IOpenable
{
    [SerializeField] private GameObject doorClosed;
    [SerializeField] private GameObject doorOpened;

    [SerializeField] private bool openStatus;
    [SerializeField] private GameObject outline;

    // Start is called before the first frame update
    private void Awake()
    {
        doorClosed.SetActive(!openStatus);
        doorOpened.SetActive(openStatus);

    }

    void Start()
    {
        // doorClosed.SetActive(!openStatus);
        // doorOpened.SetActive(openStatus);
    }

    public void SetOpen(bool openStatus)
    {
        this.openStatus = openStatus;
        doorClosed.SetActive(!openStatus);
        doorOpened.SetActive(openStatus);
    }

    public bool GetOpenStatus()
    {
        return openStatus;
    }

    public void SwapOpenState()
    {
        openStatus = !openStatus;
        doorClosed.SetActive(!openStatus);
        doorOpened.SetActive(openStatus);
    }

    public string getName()
    {
        return gameObject.name;
    }

    public void ShowOutline(bool displayMode)
    {
        if (outline != null)
        {
            outline.SetActive(displayMode);
        }
    }
}
