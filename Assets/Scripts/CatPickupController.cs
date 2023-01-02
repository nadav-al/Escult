using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CatPickupController : MonoBehaviour
{
    [SerializeField] private float throwSpeed = 3;
    [SerializeField] private Rigidbody2D rigidbody;
    private FaceDirection catDirection;
    [SerializeField] private Tilemap hell;
    [SerializeField] private GameObject girl;
    private SoulsController soulsController;

    private void Start()
    {
        soulsController = GetComponent<SoulsController>();
    }

    public void Pick()
    {
        gameObject.SetActive(false);
    }

    public void Throw(Vector3 playerLoc, FaceDirection faceDirection)
    {
        gameObject.SetActive(true);
        gameObject.layer = Layers.Air;
        catDirection = faceDirection;
        gameObject.transform.position = playerLoc;
        Vector2 throwVelocity = Vector2.zero;
        switch (faceDirection)
        {
            case FaceDirection.Left:
                throwVelocity = Vector2.left;
                break;
            case FaceDirection.Right:
                throwVelocity = Vector2.right;
                break;
            case FaceDirection.Up:
                throwVelocity = Vector2.up;
                break;
            case FaceDirection.Down:
                throwVelocity = Vector2.down;
                break;
        }
        rigidbody.velocity = throwSpeed * throwVelocity;
    }

    public void Land()
    {
        rigidbody.velocity = Vector2.zero;
        gameObject.layer = Layers.Ground;
        Vector3Int catPos = hell.WorldToCell(transform.position);
        
        if (hell.HasTile(catPos))
        {
            Debug.Log("You are in hell");
            soulsController.DecreaseSoul();
            gameObject.transform.position = girl.transform.position;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.layer == Layers.Wall)
        {
            Land();            
        }
    }

}