using UnityEngine;

public class MagicCarpet : MonoBehaviour, IStepable
{
    private bool steppedOn;
    [SerializeField] private IOpenable doorController;
    
    public void StepOn()
    {
        steppedOn = true;
        Debug.Log("Stepped On Platform");
        doorController.SetOpen(steppedOn);
    }

    public void StepOff()
    {
        steppedOn = false;
        Debug.Log("Stepped Off Platform");
        doorController.SetOpen(steppedOn);
    }

    public bool isStepped()
    {
        return steppedOn;
    }
    
    
}