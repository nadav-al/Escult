using System;
using UnityEngine;

public class fpsCheck : MonoBehaviour
{
    [SerializeField] private float delayParam1 = 1;
    private int fpsCounter = 0;
    private float fpsTime = 1;

    // Start is called before the first frame update

    // Update is called once per frame. It invokes heavy loops to delay the program and prints the estimated FPS.
    void Update()
    {
        fpsCounter++;
        fpsTime -= Time.deltaTime;

        if (fpsTime <= 0)
        {
            Debug.Log("FPS is: " + fpsCounter);
            fpsCounter = 0;
            fpsTime = 1;
        }

        for (int index1 = 0; index1 < delayParam1; index1++)
        {
            for (int index2 = 0; index2 < 1000; index2++)
            {
                int a = 2;
                var b = DateTime.Now;
            }
        }

    }
}
