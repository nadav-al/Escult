using UnityEngine;

public class CatInteractController : MonoBehaviour
{
    [SerializeField] private KeyCode interactButton;
    
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Steppable))
        {
            col.gameObject.GetComponent<IStepable>().StepOn();
        }
    }


    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag(Tags.Steppable))
        {
            col.gameObject.GetComponent<IStepable>().StepOff();
        }
    }

}
