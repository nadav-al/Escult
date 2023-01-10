using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialGUIHandler : MonoBehaviour
{
    [SerializeField] private List<TextMeshProUGUI> tutorialTextList;

    private void OnEnable()
    {
        foreach(var tutorialText in tutorialTextList)
            tutorialText.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        foreach(var tutorialText in tutorialTextList)
            tutorialText.gameObject.SetActive(false);
    }
}