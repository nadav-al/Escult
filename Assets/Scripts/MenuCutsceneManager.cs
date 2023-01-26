using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuCutsceneManager : MonoBehaviour
{
    [SerializeField] private bool isOpening;
    [SerializeField] private List<GameObject> cutscenes;
    private int currSceneInd = 0;

    private void Awake()
    {
        currSceneInd = 0;
        cutscenes[currSceneInd].SetActive(true);
    }

    // Start is called before the first frame update
    // void Start()
    // {
    //     openCutscenes[currOpenSceneInd].SetActive(true);
    // }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currSceneInd < (cutscenes.Count - 1))
            {
                cutscenes[currSceneInd].SetActive(false);
            }
        
            if (++currSceneInd == cutscenes.Count)
            {
                currSceneInd = 0;
                if (isOpening)
                {
                    SceneManager.LoadScene("SampleScene");    
                }
                else
                {
                    SceneManager.LoadScene("Start Menu Scene");
                }

                return;
            }
            cutscenes[currSceneInd].SetActive(true);
        }
    }
}
