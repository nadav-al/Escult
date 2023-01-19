using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GenerateAnimTile : MonoBehaviour
{
    public AnimationClip animationClip;

    private void Start()
    {
        // Create a new tile asset
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        
    }
}
