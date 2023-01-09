using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textSouls;
    [SerializeField] private KeyCode restartLevelKey = KeyCode.R; 
    [SerializeField] private KeyCode switchCharactersKey = KeyCode.Tab; 
    private SoulsController catSouls;

    [SerializeField] private List<LevelManager> levels;
    private int currLevelInd = 0;

    [SerializeField] private GameObject girl;
    private MovementController girlMovementCtrl;
    private GirlInteractController girlInteractCtrl;
    [SerializeField] private GameObject cat;
    private MovementController catMovementCtrl;
    private CatInteractController catInteractCtrl;
    
    // true = girl, false = cat
    private bool focusedCharacter = true;
    private CatPickupController catPickupController;


    public void NextLevel()
    {
        levels[currLevelInd].ResetLevel();
        levels[currLevelInd].gameObject.SetActive(false);
        if (++currLevelInd == levels.Count)
        {
            // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
            Debug.Log("Done");
            return;
        }
        // TODO - SHOW NADAV - Need to reset bridge list between stages 
        cat.GetComponent<CatInteractController>().ResetBridgeList();
        levels[currLevelInd].gameObject.SetActive(true);
        levels[currLevelInd].ResetLevel();
        focusedCharacter = true;
        ApplyFocusToCharacters();
    }

    // Start is called before the first frame update
    void Start()
    {
        levels[currLevelInd].SetActive(true);
        levels[currLevelInd].ResetLevel();
        girlMovementCtrl = girl.GetComponent<MovementController>();
        girlInteractCtrl = girl.GetComponent<GirlInteractController>();
        catMovementCtrl = cat.GetComponent<MovementController>();
        catInteractCtrl = cat.GetComponent<CatInteractController>();
        catPickupController = cat.GetComponent<CatPickupController>();
        catSouls = cat.GetComponent<SoulsController>();
        // textSouls = cat
        
        ApplyFocusToCharacters();
        textSouls.SetText("Remaining Souls: " + catSouls.getSouls());
    }

    // Update is called once per frame
    void Update()
    {
        textSouls.SetText("Remaining Souls: " + catSouls.getSouls());
        if (catSouls.IsDead() && cat.activeSelf)
        {
            cat.SetActive(false);
            focusedCharacter = true;
            ApplyFocusToCharacters();
        }
        if (Input.GetKeyDown(restartLevelKey))
        {
            focusedCharacter = true;
            ApplyFocusToCharacters();
            levels[currLevelInd].ResetLevel();
        }
        // TODO - SHOW NADAV HERE DROPPING CAT
        if (!catSouls.IsDead() && Input.GetKeyDown(switchCharactersKey))
        {
            if (girlInteractCtrl.GetHoldsCat())
            {
                girlInteractCtrl.SetHoldsCat(false);
                catPickupController.dropCat(girl.transform.position);
            }
            focusedCharacter = !focusedCharacter;
            ApplyFocusToCharacters();
        }
    }

    private void ApplyFocusToCharacters()
    {
        girlMovementCtrl.SetFocus(focusedCharacter);
        girlInteractCtrl.SetFocus(focusedCharacter);
        catMovementCtrl.SetFocus(!focusedCharacter);
        catInteractCtrl.SetFocus(!focusedCharacter);
    }

    public LevelManager getLevel()
    {
        return levels[currLevelInd];
    }
}
