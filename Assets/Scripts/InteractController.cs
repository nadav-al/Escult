using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractController : MonoBehaviour
{
    private const string CatTag = "Cat";

    [SerializeField] private KeyCode interactButton;
    private MovementController movementController;
    [SerializeField] private FaceDirection playerDirection;
    private GameObject potentialHeldItem;
    [SerializeField] private GameObject cat;
    private CatPickupController catController;
    private bool catInArea;
    private bool holdsCat;
    
    // Start is called before the first frame update
    void Start()
    {
        movementController = GetComponent<MovementController>();
        // catController = cat.GetComponent<CatPickupController>();;
    }

    // Update is called once per frame
    void Update()
    {
        playerDirection = movementController.faceDirection;
        if (Input.GetKeyDown(interactButton))
        {
            if (catInArea)
            {
                catController.Pick();
                holdsCat = true;
                catInArea = false;
            }
            else if (holdsCat)
            {
                catController.Throw(transform.position, movementController.faceDirection);
                holdsCat = false;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag(CatTag))
        {
            catController = col.gameObject.GetComponent<CatPickupController>();
            catInArea = true;
        }
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag(CatTag))
        {
            catInArea = false;
            // catController = null;
        }
    }
}