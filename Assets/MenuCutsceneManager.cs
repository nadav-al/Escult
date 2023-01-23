using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuCutsceneManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> openCutscenes;
    private int currOpenSceneInd = 0;
    [SerializeField] private List<GameObject> endCutscenes;
    private int currEndSceneInd = 0;

    [SerializeField] private GameObject gameManagerObj;
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
            
        }
    }
}
