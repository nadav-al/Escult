using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderGirlInteractController : MonoBehaviour
{
    [SerializeField] private GameObject gameManagerObj;
    public GameManager gameManager;

    private void Start()
    {
        gameManager = gameManagerObj.GetComponent<GameManager>();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.CompareTag(Tags.Door))
        {
            IOpenable door = col.gameObject.GetComponent<DoorController>();
            if (door.GetOpenStatus())
            {
                Debug.Log("Stage Cleared");
                gameManager.NextLevel();
                Debug.Log("AAAAAAAAAAAAAA");
                Debug.Log(gameManager);

            }
        }
    }
    
    private void OnCollisionStay2D(Collision2D col)
    {
        if (col.collider.CompareTag(Tags.Door))
        {
            IOpenable door = col.gameObject.GetComponent<DoorController>();
            if (door.GetOpenStatus())
            {
                Debug.Log("Stage Cleared");
                gameManager.NextLevel();
            }
        }
    }

}
