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
    [SerializeField] private Animator animator;

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
        animator.SetBool("CatInAir",true);
        catDirection = faceDirection;
        Vector3Int cellPosition = hell.WorldToCell(playerLoc);
        Vector3 cellCenter = hell.GetCellCenterWorld(cellPosition); 
        gameObject.transform.position = cellCenter;
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
        animator.SetBool("CatInAir",false);
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
        // TODO - only land if on air.
        if (col.gameObject.layer == Layers.Wall && this.gameObject.layer == Layers.Air)
        {
            Land();            
        }
    }

    public void setHellmap(Tilemap hellMap)
    {
        this.hell = hellMap;
    }

    public void dropCat(Vector3 playerLoc)
    {
        animator.SetBool("CatInAir",false);
        gameObject.transform.position = playerLoc;
        gameObject.SetActive(true); 
        rigidbody.velocity = Vector2.zero;
        gameObject.layer = Layers.Ground;
    }
}