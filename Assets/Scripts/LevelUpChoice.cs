using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpChoice : MonoBehaviour {

    [Header("Attributs")]
    [SerializeField] private GameObject[] banners;
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private GameObject dancePanel;

    [Header("Classes utilitaires")]
    [SerializeField] private HUDStats hudStats;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private AudioController _audio;

    private string[] bannersNames;
    private int[] bannersLevel;
    private int bannersLength = 9;
    private int marginMultiplicator;

    public void Awake() {

        this.bannersNames = new string[] {
            "attack_",
            "health_",
            "resistance_",
            "range_",
            "attack_speed_",
            "speed_",
            "regeneration_",
            "stamina_",
            "stamina_regen_"
        };
        this.EnsureStaminaBanners();
        GameObject.Find("Upgrade Interface").GetComponentInChildren<Image>().sprite = Resources.Load<Sprite>("Interface/Backgrounds/controle_" + GameController.GetSystemLanguageLower());
    }

    private void EnsureStaminaBanners() {
        if(this.banners != null && this.banners.Length >= this.bannersLength) return;
        if(this.banners == null || this.banners.Length < 7) {
            Debug.LogError("[LevelUpChoice] Les 7 bannieres d'origine doivent etre assignees.");
            this.bannersLength = (this.banners != null) ? this.banners.Length : 0;
            return;
        }

        GameObject[] expanded = new GameObject[this.bannersLength];
        Array.Copy(this.banners, expanded, this.banners.Length);

        expanded[7] = this.CreateBanner(this.banners[1], "Stamina Banner", this.ChoiceStamina);
        expanded[8] = this.CreateBanner(this.banners[6], "Stamina Regen Banner", this.ChoiceStaminaRegen);
        this.banners = expanded;
    }

    private GameObject CreateBanner(GameObject template, string objectName, UnityEngine.Events.UnityAction action) {
        GameObject banner = Instantiate(template, template.transform.parent);
        banner.name = objectName;

        Button button = banner.GetComponent<Button>();
        if(button != null) {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        GameController.SetPanelVisibility(banner, false);
        return banner;
    }

    public void UpdateStatsDisplay() {
        this.bannersLevel = new int[] {
            this.playerController.GetAttackLevel(),
            this.playerController.GetHealthLevel(),
            this.playerController.GetResistanceLevel(),
            this.playerController.GetRangeLevel(),
            this.playerController.GetAttackSpeedLevel(),
            this.playerController.GetSpeedLevel(),
            this.playerController.GetRegenerationLevel(),
            this.playerController.GetStaminaLevel(),
            this.playerController.GetStaminaRegenLevel()
        };

        List<int> isMaxLevel = new List<int>();

        for(int i = 0; i < this.bannersLevel.Length; i++) 
            if(this.bannersLevel[i] > 5) 
                isMaxLevel.Add(i);


        int[] excludes = this.GenerateUniquesRandom(0, this.bannersLength - 1, isMaxLevel.ToArray());

        this._audio.PlayLevelUpSFX();

        GameController.SetGameState(false);
        GameController.SetCanInteract(false);
        GameController.SetPanelVisibility(levelUpPanel, true);
        this.HideBanners(excludes);
        StartCoroutine(ShowCursor());
        // Cursor.position = new Vector3(targetPosition.x, targetPosition.y, 0);
    }

    private IEnumerator ShowCursor() {
        yield return new WaitForSecondsRealtime(1f);
        GameController.SetCursorVisibility(true);
    }

    public int[] GenerateUniquesRandom(int min, int max, int[] excludes) {
        HashSet<int> uniqueNumbers = new HashSet<int>(excludes);

        while(uniqueNumbers.Count < Math.Max(this.bannersLength - 3, excludes.Length)) {
            int randomNumber = GameController.Random(min, max);

            if(!uniqueNumbers.Contains(randomNumber)) 
                uniqueNumbers.Add(randomNumber);     
        }

        return uniqueNumbers.ToArray();
    }

    private int[][] GetPositionArray() {
        int bannerWidth = 175;
        int bannerHeight = 250;
        int initialXPosition = Screen.width / 2;
        int initialYPosition = Screen.height / 2 - bannerHeight / 2;
        int marginX = Convert.ToInt32(bannerWidth * 2.2f);

        int[][] positions = new int[3][];

        for(int i = 0; i < 3; i++) {
            int[] position = new int[2];
            this.marginMultiplicator = i - 1;
            position[0] = initialXPosition + this.marginMultiplicator * marginX;
            position[1] = initialYPosition;
            positions[i] = position; 
        }

        return positions;
    }

    private void HideBanners(int[] excludes) {

        int[][] positions = this.GetPositionArray();

        for(int i = 0; i < this.bannersLength; i++) 
            GameController.SetPanelVisibility(this.banners[i], false);
        

        int cpt = 0;
        for (int i = 0; i < this.bannersLength; i++) {
            if(Array.IndexOf(excludes, i) == -1) {
        
                this.banners[i].transform.position = new Vector3(positions[cpt][0], positions[cpt][1], 0);
                RectTransform rectTransform = banners[i].GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(175, 350);

                this.banners[i].GetComponent<Image>().sprite = LoadBannerSprite(this.bannersNames[i], this.bannersLevel[i]);
                GameController.SetPanelVisibility(this.banners[i], true);

                cpt++;

                if(cpt >= excludes.Length) 
                    break;    
            }
        }
    }


    void ResumeGame() {
        this.playerController.SetCanResume(true);
        this.hudStats.UpdateStats();
        
        for(int i = 0; i < this.bannersLength; i++)
            GameController.SetPanelVisibility(banners[i], true);
        
        GameController.SetGameState(true);
        GameController.SetCanInteract(true);
        GameController.SetCursorVisibility(false);
        GameController.SetPanelVisibility(levelUpPanel, false);   
        GameController.SetCanvasVisibility(this.playerController.GetQuestCanvas(), this.playerController.IsQuestCanvasVisible());
        GameController.SetPanelVisibility(this.dancePanel, true);
    }


    public void ChoiceAttack() {
        this.playerController.UpdateAttack();
        this.ResumeGame();
    }

    public void ChoiceHealth() {
        this.playerController.UpdateHealth();
        this.ResumeGame();
    }

    public void ChoiceResistance() {
        this.playerController.UpdateResistance();
        this.ResumeGame();
    }

    public void ChoiceAttackSpeed() {
        this.playerController.UpdateAttackSpeed();
        this.ResumeGame();
    }

    public void ChoiceRange() {
        this.playerController.UpdateRange();
        this.ResumeGame();
    }

    public void ChoiceSpeed() {
        this.playerController.UpdateSpeed();
        this.ResumeGame();
    }

    public void ChoiceRegeneration() {
        this.playerController.UpdateRegeneration();
        this.ResumeGame();
    }

    public void ChoiceStamina() {
        this.playerController.UpdateStamina();
        this.ResumeGame();
    }

    public void ChoiceStaminaRegen() {
        this.playerController.UpdateStaminaRegen();
        this.ResumeGame();
    }

    private Sprite LoadBannerSprite(string spriteName, int level) {
        return Resources.Load<Sprite>("Interface/Banners/" + GameController.GetSystemLanguageUpper() + "/" + spriteName + level);
    }
}
