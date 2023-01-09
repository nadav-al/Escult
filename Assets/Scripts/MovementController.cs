using System;
using UnityEngine;


public class MovementController : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 5f;
    
    [SerializeField] private KeyCode leftButtonOpt1;
    [SerializeField] private KeyCode rightButtonOpt1;
    [SerializeField] private KeyCode upButtonOpt1;
    [SerializeField] private KeyCode downButtonOpt1;
    [SerializeField] private KeyCode leftButtonOpt2;
    [SerializeField] private KeyCode rightButtonOpt2;
    [SerializeField] private KeyCode upButtonOpt2;
    [SerializeField] private KeyCode downButtonOpt2;
    
    private bool isFocused = true;
    private Rigidbody2D _rigidbody2D;
    public FaceDirection faceDirection;
    
    [SerializeField] private Animator animator;

    public void SetFocus(bool isFocused)
    {
        this.isFocused = isFocused;
    }
    // Update is called once per frame
    // It checks for input from the user and moves/rotates the ship accordingly.
    private void Start()
    {
        _rigidbody2D = this.GetComponent<Rigidbody2D>();
        // animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        animator.SetBool("WalksRight", false);
        animator.SetBool("WalksLeft", false);
        animator.SetBool("WalksUp", false);
        animator.SetBool("WalksDown", false);
        if (gameObject.layer == Layers.Air)
        {
            return;
        }
        
        if (!isFocused)
        {
            _rigidbody2D.velocity = Vector2.zero;
            return;
        }

        Vector2 newVel = Vector2.zero;
        if (Input.GetKey(upButtonOpt1) || Input.GetKey(upButtonOpt2))
        {
            animator.SetBool("WalksUp", true);
            faceDirection = FaceDirection.Up;
            newVel += Vector2.up;
        }
        if (Input.GetKey(downButtonOpt1) || Input.GetKey(downButtonOpt1))
        {
            animator.SetBool("WalksDown", true);
            faceDirection = FaceDirection.Down;
            newVel += Vector2.down;
        }

        if (Input.GetKey(rightButtonOpt1) || Input.GetKey(rightButtonOpt2))
        {
            animator.SetBool("WalksRight", true);
            faceDirection = FaceDirection.Right;
            newVel += Vector2.right;
        }
        if (Input.GetKey(leftButtonOpt1) || Input.GetKey(leftButtonOpt2))
        {
            animator.SetBool("WalksLeft", true);
            faceDirection = FaceDirection.Left;
            newVel += Vector2.left;
        }
        _rigidbody2D.velocity = movementSpeed*(newVel.normalized);
    }
}