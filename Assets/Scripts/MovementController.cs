using System;
using UnityEngine;


public class MovementController : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private KeyCode leftButton;
    [SerializeField] private KeyCode rightButton;
    [SerializeField] private KeyCode upButton;
    [SerializeField] private KeyCode downButton;
    // [SerializeField] private KeyCode interactButton;
    private bool canMoveRight = true;
    private bool canMoveLeft = true;
    private bool canMoveDown = true;
    private bool canMoveUp = true;
    private bool tryMovingRight = true;
    private bool tryMovingLeft = true;
    private bool tryMovingUp = true;
    private bool tryMovingDown = true;
    private Rigidbody2D _rigidbody2D;
    public FaceDirection faceDirection;
    // Update is called once per frame
    // It checks for input from the user and moves/rotates the ship accordingly.
    private void Start()
    {
        _rigidbody2D = this.GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        tryMovingRight = false;
        tryMovingLeft = false;
        tryMovingUp = false;
        tryMovingDown = false;
        _rigidbody2D.velocity = Vector2.zero;
        Vector2 newVel = Vector2.zero;
        if (Input.GetKey(upButton))
        {
            faceDirection = FaceDirection.Up;
            if (canMoveUp)
            {
                tryMovingUp = true;
                // transform.position += transform.up * (movementSpeed * Time.deltaTime);
                newVel += Vector2.up;
            }
        }
        if (Input.GetKey(downButton))
        {
            faceDirection = FaceDirection.Down;
            if (canMoveDown)
            {
                tryMovingDown = true;
                // transform.position -= transform.up * (movementSpeed * Time.deltaTime);
                newVel += Vector2.down;
            }
        }

        if (Input.GetKey(rightButton))
        {
            faceDirection = FaceDirection.Right;
            if (canMoveRight)
            {
                tryMovingRight = true;
                // transform.position += transform.right * (movementSpeed * Time.deltaTime);
                newVel += Vector2.right;
            }
        }
        if (Input.GetKey(leftButton))
        {
            faceDirection = FaceDirection.Left;
            if (canMoveLeft)
            {
                tryMovingLeft = true;
                // transform.position -= transform.right * (movementSpeed * Time.deltaTime);
                newVel += Vector2.left;
            }
        }
        _rigidbody2D.velocity = movementSpeed*(newVel.normalized);
    }
    
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("Hello Sibling");
        }
        // this.canMove(other.gameObject.transform.position, false);
    }
    
    private void OnCollisionExit2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("Goodbye Sibling");
        }
        // this.canMoveRight = true;
        // this.canMoveLeft = true;
        // this.canMoveUp = true;
        // this.canMoveDown = true;
    }
    // This function determines whether the object can move in up down left right direction,
    // according to collision with a given position.
    private void canMove(Vector3 otherPosition, bool canMove)
    {
        if (tryMovingLeft && this.transform.position.x > otherPosition.x)
        {
            this.canMoveLeft = canMove;
        }
        
        if (tryMovingRight && this.transform.position.x < otherPosition.x) 
        {
            this.canMoveRight = canMove;
        }
        
        if (tryMovingDown && this.transform.position.y > otherPosition.y)
        {
            this.canMoveDown = canMove;
        }
        
        if (tryMovingUp && this.transform.position.y < otherPosition.y)
        {
            this.canMoveUp = canMove;
        }    
    }

}
