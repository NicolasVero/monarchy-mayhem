using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDStats : MonoBehaviour {

    [Header("Text Mesh")]
    [SerializeField] private TextMeshProUGUI levelStat;
    [SerializeField] private TextMeshProUGUI healthStat;
    [SerializeField] private TextMeshProUGUI killStat;
    [SerializeField] private TextMeshProUGUI attackStat;
    [SerializeField] private TextMeshProUGUI attackSpeedStat;
    [SerializeField] private TextMeshProUGUI rangeStat;
    [SerializeField] private TextMeshProUGUI regenerationStat;
    [SerializeField] private TextMeshProUGUI resistanceStat;
    [SerializeField] private TextMeshProUGUI speedStat;
    [SerializeField] private TextMeshProUGUI knockbackStat;
    [SerializeField] private TextMeshProUGUI weaponName;
    [SerializeField] private TextMeshProUGUI difficultyName;

    [Header("Secondary Text Mesh")]
    [SerializeField] private TextMeshProUGUI weaponAttack;
    [SerializeField] private TextMeshProUGUI weaponAttackSpeed;
    [SerializeField] private TextMeshProUGUI weaponRange;
    [SerializeField] private TextMeshProUGUI weaponRegeneration;
    [SerializeField] private TextMeshProUGUI weaponSpeed;
    [SerializeField] private TextMeshProUGUI weaponKnockback;


    [Header("Images")]
    [SerializeField] private RawImage attackIcon;
    [SerializeField] private RawImage attackSpeedIcon;
    [SerializeField] private RawImage rangeIcon;
    [SerializeField] private RawImage regenerationIcon;
    [SerializeField] private RawImage resistanceIcon;
    [SerializeField] private RawImage speedIcon;
    [SerializeField] private RawImage knockbackIcon;
    [SerializeField] private RawImage enableAttackIcon;
    [SerializeField] private RawImage difficultyIcon;

    private Difficulty difficultyController;

    [Header("Joueur")]
    [SerializeField] private PlayerController player;

    private readonly string iconsPath = "Interface/Icons/";

    // Un avertissement par champ manquant, pas un par frame.
    private static readonly HashSet<string> reportedMissing = new HashSet<string>();


    void Start() {
        this.UpdateStats();
        this.SetDifficulty();
    }

    private void SetDifficulty() {
        this.difficultyController = FindObjectOfType<Difficulty>();
        string name = "";

        if(this.difficultyController != null) {
        
            this.difficultyController.DisableChoice();

            this.difficultyIcon.texture = Resources.Load<Texture2D>(this.iconsPath + this.difficultyController.GetDifficulty());

            if(this.difficultyController.GetDifficulty() == "easy") name = "Agitation"; 
            if(this.difficultyController.GetDifficulty() == "medium") name = "Soulèvement"; 
            if(this.difficultyController.GetDifficulty() == "hard") name = "Insurrection";
        } else {
            this.difficultyIcon.texture = Resources.Load<Texture2D>(this.iconsPath + Difficulty.Default);
            name = "Agitation";
        }

        this.difficultyName.text = name; 
    }

    // Un seul champ non assigné dans l'inspecteur levait une NullReferenceException
    // à la première ligne d'UpdateStats, et AUCUNE des stats suivantes n'était écrite :
    // tout le panneau restait figé sur le texte saisi dans la scène. Chaque écriture
    // est désormais isolée, et les champs manquants sont nommés une fois en Console.
    private static void SetText(TextMeshProUGUI field, string value, string fieldName) {

        if(field == null) {
            if(reportedMissing.Add(fieldName))
                Debug.LogWarning("[HUDStats] Le champ \"" + fieldName + "\" n'est pas assigné dans l'inspecteur : cette stat ne s'affichera pas.");

            return;
        }

        field.text = value;
    }

    private static void SetDelta(TextMeshProUGUI field, float value, string fieldName, bool lowerIsBetter = false) {

        if(field == null) {
            if(reportedMissing.Add(fieldName))
                Debug.LogWarning("[HUDStats] Le champ \"" + fieldName + "\" n'est pas assigné dans l'inspecteur : cette stat ne s'affichera pas.");

            return;
        }

        if(value == 0f) {
            field.text = "";
            return;
        }

        field.text = "" + value;
        field.color = (lowerIsBetter ? value < 0f : value > 0f) ? Color.green : Color.red;
    }

    public void UpdateHealth() {
        if(this.player == null) return;

        int health = Mathf.Max(this.player.GetHealth(), 0);
        SetText(this.healthStat, health + " / " + this.player.GetMaxActualHealth(), "healthStat");
    }

    public void UpdateStats() {

        if(this.player == null) return;

        this.UpdateHealth();

        SetText(this.levelStat,        "" + this.player.GetLevel(),                        "levelStat");
        SetText(this.killStat,         "" + this.player.GetKillCounter(),                  "killStat");
        SetText(this.attackStat,       "" + this.player.GetAttack(),                       "attackStat");
        SetText(this.attackSpeedStat,  this.player.GetAttackSpeed().ToString("F1"),        "attackSpeedStat");
        SetText(this.rangeStat,        "" + this.player.GetRange(),                        "rangeStat");
        SetText(this.resistanceStat,   "" + this.player.GetResistance(),                   "resistanceStat");
        SetText(this.speedStat,        "" + this.player.GetSpeed(),                        "speedStat");
        SetText(this.knockbackStat,    "" + this.player.GetKnockback(),                    "knockbackStat");
        SetText(this.regenerationStat, "" + this.player.GetRegeneration(),                 "regenerationStat");
        string displayedWeaponName = this.player.GetWeaponName();
        int weaponAmmo = this.player.GetWeaponAmmo();

        if(!string.IsNullOrEmpty(displayedWeaponName) && weaponAmmo > 0)
            displayedWeaponName += "  x" + weaponAmmo;

        SetText(this.weaponName,       displayedWeaponName,                               "weaponName");

        // Bonus d'arme : vert quand c'est un gain. Pour la vitesse d'attaque, qui est
        // un délai, un chiffre négatif est un gain.
        SetDelta(this.weaponAttack,       this.player.GetWeaponAttack(),       "weaponAttack");
        SetDelta(this.weaponRange,        this.player.GetWeaponRange(),        "weaponRange");
        SetDelta(this.weaponAttackSpeed,  this.player.GetWeaponAttackSpeed(),  "weaponAttackSpeed", true);
        SetDelta(this.weaponSpeed,        this.player.GetWeaponSpeed(),        "weaponSpeed");
        SetDelta(this.weaponRegeneration, this.player.GetWeaponRegeneration(), "weaponRegeneration");
        SetDelta(this.weaponKnockback,    this.player.GetWeaponKnockback(),    "weaponKnockback");
    }
    
    public void MaxAttack() {
        Texture2D attackTexture = Resources.Load<Texture2D>(this.iconsPath + "max_attack");
        if(attackTexture != null) {
            this.attackIcon.texture = attackTexture;
        }
    }

    public void MaxAttackSpeed() {
        Texture2D attackSpeedTexture = Resources.Load<Texture2D>(this.iconsPath + "max_attack_speed");
        if(attackSpeedTexture != null) {
            this.attackSpeedIcon.texture = attackSpeedTexture;
        }
    }

    public void MaxRange() {
        Texture2D rangeTexture = Resources.Load<Texture2D>(this.iconsPath + "max_range");
        if(rangeTexture != null) {
            this.rangeIcon.texture = rangeTexture;
        }
    }

    public void MaxResistance() {
        Texture2D resistanceTexture = Resources.Load<Texture2D>(this.iconsPath + "max_resistance");
        if(resistanceTexture != null) {
            this.resistanceIcon.texture = resistanceTexture;
        }
    }

    public void MaxSpeed() {
        Texture2D speedTexture = Resources.Load<Texture2D>(this.iconsPath + "max_speed");
        if(speedTexture != null) {
            this.speedIcon.texture = speedTexture;
        }
    }

    public void MaxRegeneration() {
        Texture2D regenerationTexture = Resources.Load<Texture2D>(this.iconsPath + "max_regeneration");
        if(regenerationTexture != null) {
            this.regenerationIcon.texture = regenerationTexture;
        }
    }

    public void MaxKnockback() {
        Texture2D knockbackTexture = Resources.Load<Texture2D>(this.iconsPath + "max_knockback");
        if(knockbackTexture != null) {
            this.knockbackIcon.texture = knockbackTexture;
        }
    }

    public void ChangeEnableAttackIcon(bool status) {
        string textureLink = status ? "Interface/Icons/can_attack" : "Interface/Icons/cant_attack";
        Texture2D enableAttackTexture = Resources.Load<Texture2D>(textureLink);

        this.enableAttackIcon.texture = enableAttackTexture;
    }
}
