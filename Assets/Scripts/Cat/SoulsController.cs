using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SoulsController : MonoBehaviour
{
    [SerializeField] private int CAT_INIT_SOULS = 9;
    private int numSouls = 9;

    [SerializeField] private Animator catAnimator;

    // Start is called before the first frame update
    private void Awake()
    {
        numSouls = CAT_INIT_SOULS;
    }

    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void DecreaseSoul()
    {
        numSouls--;
        catAnimator.SetInteger("CatSouls", numSouls);
    }

    public void IncreaseSoul()
    {
        numSouls++;
    }

    public bool IsDead()
    {
        return numSouls <= 0;
    }

    public int getSouls()
    {
        return numSouls;
    }

    public void ResetSouls()
    {
        numSouls = CAT_INIT_SOULS;
        catAnimator.SetInteger("CatSouls", numSouls);
    }
}
