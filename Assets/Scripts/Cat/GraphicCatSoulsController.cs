using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GraphicCatSoulsController : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private List<Image> lifeCellsList;
    [SerializeField] private Sprite activeCellImage;
    [SerializeField] private Sprite inactiveCellImage;
    [SerializeField] private GameObject graphicLifeCounter;
    private int currCell;
    void Awake()
    {
        currCell = 9;
    }

    public void ResetLifeCellsList()
    {
        foreach (Image cellImage in lifeCellsList)
        {
            cellImage.sprite = activeCellImage;
        }
    }

    public void SetCellImgInactive(int cellNum)
    {
        lifeCellsList[cellNum].sprite = inactiveCellImage;
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowHealthBar(bool isActive)
    {
        graphicLifeCounter.SetActive(isActive);
    }
}
