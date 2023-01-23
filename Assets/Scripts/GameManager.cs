using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textSouls;
    [SerializeField] private Animator activeCharacterAnimator;
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
    private ColliderGirlInteractController girlColliderInteractCtrl;
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


    // this semaphore helps us to decide if we are in an important animation

    // in each script we activer
    private int importantAnimationsSempahore = 0;

    public void up()
    {
        importantAnimationsSempahore+=1;
    }

    public void down()
    {
        importantAnimationsSempahore-=1;
    }

    public bool isImportantAnimationsPlaying()
    {
        return girlInteractCtrl.isImportantAnimationPlaying() || girlColliderInteractCtrl.isImportantAnimationPlaying()
               || catInteractCtrl.isImportantAnimationPlaying() ||catPickupController.isImportantAnimationPlaying();
        return importantAnimationsSempahore > 0;
    }

    public void NextLevel()
    {
        // levels[currLevelInd].ResetLevel();
        levels[currLevelInd].SetActive(false);
        
        if (++currLevelInd == levels.Count)
        {
            // TODO turn off all other objects that are not relevant for Game Over screen (like the cat lives).
            Debug.Log("Done");
            return;
        } 
        // TODO - why did we put it here and not in resetLevel?? Because we need the coordinates of original
        // hell tile maps.
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

    public bool isCatDead()
    {
        return catSouls.IsDead();
    }

    public void SetFocusedCharacter(bool focusValue)
    {
        focusedCharacter = focusValue;
    }
    public void ApplyFocusToCharacters()
    {
        activeCharacterAnimator.SetBool("FocusedCharacter", focusedCharacter);
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
        StartGame();
    }

    public void StartGame()
    {
        catAnimator.Rebind();
        girlAnimator.Rebind();
        catAnimator.SetInteger("CatSouls", 9);
        
        levels[currLevelInd].SetActive(true);
        girlMovementCtrl = girl.GetComponent<MovementController>();
        girlInteractCtrl = girl.GetComponent<GirlInteractController>();
        girlColliderInteractCtrl = girl.GetComponentInChildren<ColliderGirlInteractController>();
        catMovementCtrl = cat.GetComponent<MovementController>();
        catInteractCtrl = cat.GetComponent<CatInteractController>();
        catPickupController = cat.GetComponent<CatPickupController>();
        catSouls = cat.GetComponent<SoulsController>();

        girlRenderer = girl.GetComponent<SpriteRenderer>();
        catRenderer = cat.GetComponent<SpriteRenderer>();
        girlActiveColor = girlRenderer.color;
        girlInactiveColor = girlActiveColor * inactiveOpacityFactor;
        catActiveColor = catRenderer.color;
        catInactiveColor = catActiveColor * inactiveOpacityFactor;
        
        ApplyFocusToCharacters();
        textSouls.SetText("Remaining Souls: " + catSouls.getSouls());
        levels[currLevelInd].StartNewLevel();
    }

    // Update is called once per frame
    void Update()
    {
        textSouls.SetText("Remaining Souls: " + catSouls.getSouls());
        if (catSouls.IsDead() && !isImportantAnimationsPlaying())
        {
            importantAnimationsSempahore = 0;
            focusedCharacter = true;
            ApplyFocusToCharacters();
        }
        // if (catSouls.IsDead())
        // {
        //     focusedCharacter = true;
        //     ApplyFocusToCharacters();
        // }
        if (Input.GetKeyDown(restartLevelKey))
        {
            importantAnimationsSempahore = 0;
            focusedCharacter = true;
            ApplyFocusToCharacters();
            catAnimator.Rebind();
            girlAnimator.Rebind();
            catAnimator.SetInteger("CatSouls", 9);
            levels[currLevelInd].ResetLevel();
        }
        if (levels[currLevelInd].getCatInLevel() && !catSouls.IsDead() && !isImportantAnimationsPlaying() &&
            (Input.GetKeyDown(switchCharactersKeyOpt1) || Input.GetKeyDown(switchCharactersKeyOpt2)))
        {
            if (girlInteractCtrl.GetHoldsCat())
            {
                girlInteractCtrl.SetHoldsCat(false);
                catPickupController.dropCat(girl.transform.position);
                catMovementCtrl.faceDirection = FaceDirection.Down;
            }
            focusedCharacter = !focusedCharacter;
            ApplyFocusToCharacters();
        }
    }
}
