using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuCutsceneManager : MonoBehaviour
{
    [SerializeField] private bool isInOpenScenes = true; 
    [SerializeField] private List<GameObject> openCutscenes;
    private int currOpenSceneInd = 0;
    [SerializeField] private List<GameObject> endCutscenes;
    private int currEndSceneInd = 0;
    
    // Start is called before the first frame update
    void Start()
    {
        openCutscenes[currOpenSceneInd].SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isInOpenScenes)
            {
                handleOpenScenes();
            }
            else
            {
                handleEndScenes();
            }    
        }
    }

    public void PlayEndCutscens()
    {
        isInOpenScenes = false;
    }
    
    private void handleOpenScenes()
    {
        if (currOpenSceneInd < (openCutscenes.Count - 1))
        {
            openCutscenes[currOpenSceneInd].SetActive(false);
        }
        
        if (++currOpenSceneInd == openCutscenes.Count)
        {
            // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
            Debug.Log("Done");
            openCutscenes[currOpenSceneInd-1].GetComponent<Animator>().Play("FadeOut");
            SceneManager.LoadScene("SampleScene");
            return;
        }
        openCutscenes[currOpenSceneInd].SetActive(true);
    }
    private void handleEndScenes()
    {
        if (currEndSceneInd < (endCutscenes.Count - 1))
        {
            endCutscenes[currEndSceneInd].SetActive(false);
        }

        if (++currEndSceneInd >= endCutscenes.Count)
        {
            // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
            Debug.Log("Done");
            SceneManager.LoadScene("Start Menu Scene");
            return;
        } 
        endCutscenes[currEndSceneInd].SetActive(true);
    }

    
}
