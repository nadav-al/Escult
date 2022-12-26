using UnityEngine;

public class MagicCarpet : MonoBehaviour, IStepable
{
    private bool steppedOn;
    [SerializeField] private DoorController doorController;
    
    public void StepOn()
    {
        steppedOn = true;
        Debug.Log("Stepped On Platform");
        doorController.setOpen(steppedOn);
    }

    public void StepOff()
    {
        steppedOn = false;
        Debug.Log("Stepped Off Platform");
        doorController.setOpen(steppedOn);
    }

    public bool isStepped()
    {
        return steppedOn;
    }
    
    
}