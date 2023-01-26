using UnityEngine;

public class SoulsController : MonoBehaviour
{
    [SerializeField] private int CAT_INIT_SOULS = 9;
    private int numSouls = 9;

    [SerializeField] private Animator catAnimator;
    [SerializeField] private GraphicCatSoulsController graphicCatSoulsController;

    // Start is called before the first frame update
    private void Awake()
    {
        numSouls = CAT_INIT_SOULS;
    }

    public void DecreaseSoul()
    {
        numSouls--;
        catAnimator.SetInteger("CatSouls", numSouls);
        graphicCatSoulsController.SetCellImgInactive(numSouls);
    }

    public void IncreaseSoul()
    {
        numSouls++;
    }

    public bool IsDead()
    {
        return numSouls <= 0;
    }

    public int getSouls()
    {
        return numSouls;
    }

    public void ResetSouls()
    {
        numSouls = CAT_INIT_SOULS;
        catAnimator.SetInteger("CatSouls", numSouls);
        graphicCatSoulsController.ResetLifeCellsList();
    }
}
