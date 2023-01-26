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
    private const float ActiveOpacity = (float) 200/255;
    private const float InactiveOpacity = 1.0f;
    public void ResetLifeCellsList()
    {
        for (int i = 0; i < lifeCellsImageList.Count; i++)
        {
            lifeCellsImageList[i].sprite = activeCellImage;
            lifeCellsAnimatorList[i].enabled = true;
            Color color = lifeCellsImageList[i].color;
            color.a = ActiveOpacity;
            lifeCellsImageList[i].color = color;
        }
    }
    
    public void SetCellImgInactive(int cellNum)
    {
        lifeCellsAnimatorList[cellNum].enabled = false;
        lifeCellsImageList[cellNum].sprite = inactiveCellImage;
        Color color = lifeCellsImageList[cellNum].color;
        color.a = InactiveOpacity;
        lifeCellsImageList[cellNum].color = color;
    }

    public void ShowHealthBar(bool isActive)
    {
        ResetLifeCellsList();
        graphicLifeCounter.SetActive(isActive);
    }
}