using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SoulsController : MonoBehaviour
{
    [SerializeField] private int CAT_INIT_SOULS = 9;
    private int numSouls = 9;
    // Start is called before the first frame update
    void Start()
    {
        numSouls = CAT_INIT_SOULS;
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void DecreaseSoul()
    {
        numSouls--;
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
    }
}
