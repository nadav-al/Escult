using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GirlInteractController : MonoBehaviour
{
    [SerializeField] private KeyCode interactButtonOpt1 = KeyCode.E;
    [SerializeField] private KeyCode interactButtonOpt2 = KeyCode.J;
    private MovementController movementController;
    private FaceDirection girlDirection;
    private GameObject potentialHeldItem;
    private CatPickupController catController;
    private bool catInArea;
    private bool holdsCat;
    private bool isFocused;
    
    public void SetFocus(bool isFocused)
    {
        this.isFocused = isFocused;
    }
    public void SetHoldsCat(bool isHoldingCat)
    {
        holdsCat = isHoldingCat;
    }
    public bool GetHoldsCat()
    {
        return holdsCat;
    }

    // Start is called before the first frame update
    void Start()
    {
        movementController = GetComponentInParent<MovementController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!isFocused)
        {
            return;
        }
        girlDirection = movementController.faceDirection;
        if (Input.GetKeyDown(interactButtonOpt1) || Input.GetKeyDown(interactButtonOpt2))
        {
            if (catInArea)
            {
                catController.Pick();
                holdsCat = true;
                catInArea = false;
            }
            else if (holdsCat)
            {
                holdsCat = false;
                catController.Throw(transform.position, movementController.faceDirection);
            }
        }
    }


    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Cat))
        {
            catController = col.gameObject.GetComponent<CatPickupController>();
            catInArea = true;
        }
        if (col.CompareTag(Tags.Steppable))
        {
            col.gameObject.GetComponent<IStepable>().StepOn();
        }
    }


    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Cat))
        {
            catInArea = false;
        }

        if (col.CompareTag(Tags.Steppable))
        {
            col.gameObject.GetComponent<IStepable>().StepOff();
        }
    }
}