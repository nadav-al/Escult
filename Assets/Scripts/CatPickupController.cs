using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatPickupController : MonoBehaviour
{
    [SerializeField] private float throwSpeed = 3;
    [SerializeField] private Rigidbody2D rigidbody;
    private FaceDirection catDirection;
        
    private const string CatLayerOnGround = "Ground";
    private const string CatLayerInAir = "Air";
    private const string WallTag = "Wall";



    private void Start()
    {
        // rigidbody = gameObject.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        Debug.Log("Cat speed: " + rigidbody.velocity);
    }

    public void Pick()
    {
        gameObject.SetActive(false);
    }

    public void Throw(Vector3 playerLoc, FaceDirection faceDirection)
    {
        gameObject.layer = LayerMask.NameToLayer(CatLayerInAir);
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
        gameObject.SetActive(true);
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.CompareTag(WallTag))
        {
            gameObject.layer = LayerMask.NameToLayer(CatLayerOnGround);
            rigidbody.velocity = Vector2.zero;
        }
        
    }

}
