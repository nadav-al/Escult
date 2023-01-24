using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialGUIHandler : MonoBehaviour
{
    [SerializeField] private List<GameObject> tutorialList;

    private void OnEnable()
    {
        foreach(var tutorial in tutorialList)
            tutorial.SetActive(true);
    }

    private void OnDisable()
    {
        foreach(var tutorial in tutorialList)
            tutorial.SetActive(false);
    }
}