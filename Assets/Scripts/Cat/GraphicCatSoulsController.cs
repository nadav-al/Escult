using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GraphicCatSoulsController : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private List<Image> lifeCellsImageList;
    [SerializeField] private List<Animator> lifeCellsAnimatorList;
    [SerializeField] private Sprite activeCellImage;
    [SerializeField] private Sprite inactiveCellImage;
    [SerializeField] private GameObject graphicLifeCounter;
    // private List<Image> lifeCellsImageList;
    // private List<Animator> lifeCellsAnimatorList;
    private int currCell;
    void Awake()
    {
        currCell = 9;
        // foreach (GameObject lifeCell in lifeCellsList)
        // {
        //     lifeCellsImageList.Add(lifeCell.GetComponent<Image>());
        //     lifeCellsAnimatorList.Add(lifeCell.GetComponent<Animator>());
        // }
    }

    public void ResetLifeCellsList()
    {
        for (int i = 0; i < lifeCellsImageList.Count; i++)
        {
            lifeCellsImageList[i].sprite = activeCellImage;
            lifeCellsAnimatorList[i].enabled = true;
            Color color = lifeCellsImageList[i].color;
            color.a = (float) 200/255;
            lifeCellsImageList[i].color = color;
        }
    }
    
    public void SetCellImgInactive(int cellNum)
    {
        lifeCellsAnimatorList[cellNum].enabled = false;
        lifeCellsImageList[cellNum].sprite = inactiveCellImage;
        Color color = lifeCellsImageList[cellNum].color;
        if (cellNum == 0)
        {
            
        }
        else
        {
            color.a = 1;
        }
        lifeCellsImageList[cellNum].color = color;
    }

    public void ShowHealthBar(bool isActive)
    {
        ResetLifeCellsList();
        graphicLifeCounter.SetActive(isActive);
    }
}