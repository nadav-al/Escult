using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textSouls;
    [SerializeField] private Animator activeCharacterAnimator;
    [SerializeField] private KeyCode restartLevelKey = KeyCode.R; 
    [SerializeField] private KeyCode switchCharactersKeyOpt1 = KeyCode.Tab;
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
    [SerializeField] private AudioSource coinSound;

    private Color girlInactiveColor;
    private Color catInactiveColor;
    private Color girlActiveColor;
    private Color catActiveColor;


    // true = girl, false = cat
    private bool focusedCharacter = true;
    private CatPickupController catPickupController;

    private float totalTimeTaken;
    private float currLevelTimeTaken;


    public bool isImportantAnimationsPlaying()
    {
        return girlInteractCtrl.isImportantAnimationPlaying() || girlColliderInteractCtrl.isImportantAnimationPlaying()
                                                              || catInteractCtrl.isImportantAnimationPlaying() || catPickupController.isImportantAnimationPlaying();
    }

    public void ResetImportantAnimations()
    {
        girlInteractCtrl.ResetImportantAnimation();
        girlColliderInteractCtrl.ResetImportantAnimation();
        catInteractCtrl.ResetImportantAnimation();
        catPickupController.ResetImportantAnimation();
    }

    public void NextLevel()
    {
        // Debug.Log("#" + currLevelInd + " " +levels[currLevelInd].gameObject.name + ": played for " +
        //           (int)(currLevelTimeTaken/60) + " minutes and " + (int)(currLevelTimeTaken%60) + " seconds.");
        Debug.Log(levels[currLevelInd].gameObject.name + ": " +
                  (int)(currLevelTimeTaken/60) + "m " + (int)(currLevelTimeTaken%60) + "s.");
        if (++currLevelInd == levels.Count)
        {
            gameObject.SetActive(false);
            catInteractCtrl.getRidOfMovingBridges();
            totalTimeTaken += currLevelTimeTaken;
            // if (totalTimeTaken / 60 > 0)
            // {
            //     Debug.Log("Total time taken: " + (int)(totalTimeTaken/60) + " minutes and " + 
            //               (int)(totalTimeTaken%60) + " seconds.");
            // } else {
            //     Debug.Log("Total time taken: " + (int)totalTimeTaken + " seconds.");
            //
            // }
            // Debug.Log("Total time taken: " + (int)(totalTimeTaken/60) + " minutes and " + 
            //           (int)(totalTimeTaken%60) + " seconds.");
            Debug.Log("Total time taken: " + (int)(totalTimeTaken/60) + "m " + 
                      (int)(totalTimeTaken%60) + "s.");
            Debug.Log("----------------------------------\n");
            SceneManager.LoadScene("End Cutscenes Scene");
            return; 
        }
        ResetImportantAnimations();
        // levels[currLevelInd].ResetLevel();
        levels[currLevelInd - 1].DestroyLevelOutlines();
        levels[currLevelInd - 1].SetActive(false);
        
        // hell tile maps.
        cat.GetComponent<CatInteractController>().ResetBridgeList();
        levels[currLevelInd].gameObject.SetActive(true);
        levels[currLevelInd].StartNewLevel();
        focusedCharacter = true;
        ApplyFocusToCharacters();
        totalTimeTaken += currLevelTimeTaken;
        currLevelTimeTaken = 0;
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
    private void Awake()
    {
        currLevelInd = 0;
        Debug.Log("       [" + DateTime.Now + "]       ");
    }

    void Start()
    {
        StartGame();
    }

    public void StartGame()
    {
        currLevelInd = 0;
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
        totalTimeTaken = 0;
        currLevelTimeTaken = 0;
    }

    // Update is called once per frame
    void Update()
    {
        currLevelTimeTaken += Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Debug.Log("#" + currLevelInd + " " + levels[currLevelInd].gameObject.name + ": Quit before finishing");
            // Debug.Log("Total finished levels time taken: " + (int)(totalTimeTaken/60) + " minutes and " + (int)(totalTimeTaken%60) + " seconds.");
            Debug.Log(levels[currLevelInd].gameObject.name + ": Quit before finishing");
            Debug.Log("Total time taken: " + (int)(totalTimeTaken/60) + "m " + 
                      (int)(totalTimeTaken%60) + "s.");
            // totalTimeTaken += currLevelTimeTaken;
            // Debug.Log("Total time taken: " + Convert.ToInt32(totalTimeTaken%60) + " seconds.");
            Debug.Log("----------------------------------\n");
            Application.Quit();
        }
        textSouls.SetText("Remaining Souls: " + catSouls.getSouls());
        if (catSouls.IsDead() && !isImportantAnimationsPlaying())
        {
            focusedCharacter = true;
            ApplyFocusToCharacters();
        }
        if (Input.GetKeyDown(restartLevelKey))
        {
            ResetImportantAnimations();
            focusedCharacter = true;
            ApplyFocusToCharacters();
            catAnimator.Rebind();
            girlAnimator.Rebind();
            catAnimator.SetInteger("CatSouls", 9);
            levels[currLevelInd].ResetLevel();
        }
        if (currLevelInd < levels.Count && levels[currLevelInd].getCatInLevel() && !catSouls.IsDead() 
            && !isImportantAnimationsPlaying() && Input.GetKeyDown(switchCharactersKeyOpt1))
        {
            coinSound.Play();
            if (!focusedCharacter)
            {
                catMovementCtrl.StopMovementSound();
            }
            else
            {
                girlMovementCtrl.StopMovementSound();
            }
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