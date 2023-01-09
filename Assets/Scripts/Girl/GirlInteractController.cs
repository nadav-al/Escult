using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GirlInteractController : MonoBehaviour
{
    private const string CatTag = "Cat";
    private const string DoorTag = "Door";
    private const string SteppableTag = "Steppable";

    [SerializeField] private KeyCode interactButton;
    private MovementController movementController;
    [SerializeField] private FaceDirection girlDirection;
    private GameObject potentialHeldItem;
    [SerializeField] private GameObject cat;
    private CatPickupController catController;
    private bool catInArea;
    private bool holdsCat;
    private bool isFocused;
    [SerializeField] private GameObject gameManagerObj;
    private GameManager gameManager;

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
        gameManager = gameManagerObj.GetComponent<GameManager>();
        // catController = cat.GetComponent<CatPickupController>();;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isFocused)
        {
            return;
        }
        girlDirection = movementController.faceDirection;
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
                holdsCat = false;
                catController.Throw(transform.position, movementController.faceDirection);
            }
        }
    }

    // private void OnCollisionEnter2D(Collision2D col)
    // {
    //     if (col.collider.CompareTag(Tags.Door))
    //     {
    //         IOpenable door = col.gameObject.GetComponent<DoorController>();
    //         if (door.GetOpenStatus())
    //         {
    //             Debug.Log("Stage Cleared");
    //             gameManager.NextLevel();
    //             // gameObject.SetActive(false);
    //             // cat.SetActive(false);
    //         }
    //     }
    // }
    //
    // private void OnCollisionStay2D(Collision2D col)
    // {
    //     if (col.collider.CompareTag(Tags.Door))
    //     {
    //         IOpenable door = col.gameObject.GetComponent<DoorController>();
    //         if (door.GetOpenStatus())
    //         {
    //             Debug.Log("Stage Cleared");
    //             gameManager.NextLevel();
    //             // gameObject.SetActive(false);
    //             // cat.SetActive(false);
    //         }
    //     }
    // }


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
            // catController = null;
        }

        if (col.CompareTag(Tags.Steppable))
        {
            col.gameObject.GetComponent<IStepable>().StepOff();
        }
    }
}