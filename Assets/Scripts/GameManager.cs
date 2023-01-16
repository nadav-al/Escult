using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textSouls;
    [SerializeField] private KeyCode restartLevelKey = KeyCode.R; 
    [SerializeField] private KeyCode switchCharactersKeyOpt1 = KeyCode.Tab;
    [SerializeField] private KeyCode switchCharactersKeyOpt2 = KeyCode.J; 
    private SoulsController catSouls;

    [SerializeField] private List<LevelManager> levels;
    private int currLevelInd = 0;

    [SerializeField] private float inactiveOpacityFactor = 0.5f; 
    [SerializeField] private GameObject girl;
    private MovementController girlMovementCtrl;
    private GirlInteractController girlInteractCtrl;
    private SpriteRenderer girlRenderer;
    [SerializeField] private Animator girlAnimator;
    [SerializeField] private GameObject cat;
    private MovementController catMovementCtrl;
    private CatInteractController catInteractCtrl;
    private SpriteRenderer catRenderer;
    [SerializeField] private Animator catAnimator;

    private List<String> animNames;

    private Color girlInactiveColor; 
    private Color catInactiveColor; 
    private Color girlActiveColor; 
    private Color catActiveColor; 
    
    // true = girl, false = cat
    private bool focusedCharacter = true;
    private CatPickupController catPickupController;

    public bool isImportantAnimationsPlaying()
    {
        /*
        var girlAnimInfo = girlAnimator.GetCurrentAnimatorStateInfo(0);
        var catAnimInfo = catAnimator.GetCurrentAnimatorStateInfo(0);
        return animHashes.Contains(girlAnimInfo.shortNameHash) || animHashes.Contains(catAnimInfo.shortNameHash);
        */
        
        /*
        var girlAnimName = girlAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
        if (cat.activeSelf)
        {
            var catAnimName = catAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
            return animNames.Contains(girlAnimName) || animNames.Contains(catAnimName);
        }
        return animNames.Contains(girlAnimName);
        */
        
        var girlAnimInfo = girlAnimator.GetCurrentAnimatorStateInfo(0);

        foreach (var name in animNames)
        {
            if (girlAnimInfo.IsName(name) || (catAnimator.GetCurrentAnimatorStateInfo(0).IsName(name)))
            {
                return true;
            }
        }
        return false;
    }

    public void NextLevel()
    {
        // levels[currLevelInd].ResetLevel();
        levels[currLevelInd].gameObject.SetActive(false);
        if (++currLevelInd == levels.Count)
        {
            // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
            Debug.Log("Done");
            return;
        } 
        cat.GetComponent<CatInteractController>().ResetBridgeList();
        levels[currLevelInd].gameObject.SetActive(true);
        levels[currLevelInd].StartNewLevel();
        focusedCharacter = true;
        ApplyFocusToCharacters();
    }
    public LevelManager getLevel()
    {
        return levels[currLevelInd];
    }

    public void SetFocusedCharacter(bool focusValue)
    {
        focusedCharacter = focusValue;
    }
    public void ApplyFocusToCharacters()
    {
        girlMovementCtrl.SetFocus(focusedCharacter);
        girlInteractCtrl.SetFocus(focusedCharacter);
        catMovementCtrl.SetFocus(!focusedCharacter);
        catInteractCtrl.SetFocus(!focusedCharacter);
        ApplyFocusColorsToCharacters();
    }

    public void ApplyFocusColorsToCharacters()
    {
        setColorToGirl(focusedCharacter);
        setColorToCat(!focusedCharacter);
    }

    public void setColorToCat(bool isActiveColor)
    {
        catRenderer.color = isActiveColor ? catActiveColor : catInactiveColor;
    }
    public void setColorToGirl(bool isActiveColor)
    {
        girlRenderer.color = isActiveColor ? girlActiveColor : girlInactiveColor;
    }

    // Start is called before the first frame update
    void Start()
    {
        animNames = new List<String>
        {
            AnimationNames.ThrowLeft,
            AnimationNames.ThrowRight,
            AnimationNames.ThrowUp,
            AnimationNames.ThrowDown,
            AnimationNames.DeathState,
            AnimationNames.ReviveState
        };
        catAnimator.Rebind();
        girlAnimator.Rebind();
        catAnimator.SetInteger("CatSouls", 9);
        
        levels[currLevelInd].SetActive(true);
        levels[currLevelInd].StartNewLevel();
        girlMovementCtrl = girl.GetComponent<MovementController>();
        girlInteractCtrl = girl.GetComponent<GirlInteractController>();
        catMovementCtrl = cat.GetComponent<MovementController>();
        catInteractCtrl = cat.GetComponent<CatInteractController>();
        catPickupController = cat.GetComponent<CatPickupController>();
        catSouls = cat.GetComponent<SoulsController>();
        // textSouls = cat

        girlRenderer = girl.GetComponent<SpriteRenderer>();
        catRenderer = cat.GetComponent<SpriteRenderer>();
        girlActiveColor = girlRenderer.color;
        girlInactiveColor = girlActiveColor * inactiveOpacityFactor;
        catActiveColor = catRenderer.color;
        catInactiveColor = catActiveColor * inactiveOpacityFactor;
        
        ApplyFocusToCharacters();
        textSouls.SetText("Remaining Souls: " + catSouls.getSouls());
    }

    // Update is called once per frame
    void Update()
    {
        textSouls.SetText("Remaining Souls: " + catSouls.getSouls());
        // if (catSouls.IsDead() && cat.activeSelf && !isImportantAnimationsPlaying())
        // {
        //     cat.SetActive(false);
        //     focusedCharacter = true;
        //     ApplyFocusToCharacters();
        // }
        if (catSouls.IsDead())
        {
            focusedCharacter = true;
            ApplyFocusToCharacters();
        }
        if (Input.GetKeyDown(restartLevelKey))
        {
            
            focusedCharacter = true;
            ApplyFocusToCharacters();
            levels[currLevelInd].ResetLevel();
            catAnimator.Rebind();
            girlAnimator.Rebind();
            catAnimator.SetInteger("CatSouls", 9);
            // girlAnimator.SetTrigger("ToDefault");
            // catAnimator.SetTrigger("ToDefault");
        }
        if (levels[currLevelInd].getCatInLevel() && !catSouls.IsDead() && !isImportantAnimationsPlaying() &&
            (Input.GetKeyDown(switchCharactersKeyOpt1) || Input.GetKeyDown(switchCharactersKeyOpt2)))
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
}
