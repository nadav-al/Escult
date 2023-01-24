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
                // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
                Debug.Log("Done");
                // cutscenes[currSceneInd-1].GetComponent<Animator>().Play("FadeOut");
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
    
    // private void handleOpenScenes()
    // {
    //     if (currSceneInd < (cutscenes.Count - 1))
    //     {
    //         cutscenes[currSceneInd].SetActive(false);
    //     }
    //     
    //     if (++currSceneInd == cutscenes.Count)
    //     {
    //         // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
    //         Debug.Log("Done");
    //         cutscenes[currSceneInd-1].GetComponent<Animator>().Play("FadeOut");
    //         SceneManager.LoadScene("SampleScene");
    //         return;
    //     }
    //     cutscenes[currSceneInd].SetActive(true);
    // }
    // private void handleEndScenes()
    // {
    //     if (currEndSceneInd < (endCutscenes.Count - 1))
    //     {
    //         endCutscenes[currEndSceneInd].SetActive(false);
    //     }
    //
    //     if (++currEndSceneInd >= endCutscenes.Count)
    //     {
    //         // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
    //         Debug.Log("Done");
    //         SceneManager.LoadScene("Start Menu Scene");
    //         return;
    //     } 
    //     endCutscenes[currEndSceneInd].SetActive(true);
    // }

    
}
