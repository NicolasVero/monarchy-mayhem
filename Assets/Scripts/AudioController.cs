using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour {
    
    private int randomTheme = -1;
    private int randomBossTheme = 0;
    private int randomMenuSFX = -1;
    
    [Header("Sound Effects")]
    [SerializeField] AudioSource[] mainTheme;
    [SerializeField] AudioSource[] menuSFX;
    [SerializeField] AudioSource[] slashSFX;
    [SerializeField] AudioSource[] bossTheme;
    [SerializeField] AudioSource pickUpSFX;
    [SerializeField] AudioSource deathSFX;
    [SerializeField] AudioSource pauseMenuSFX;
    [SerializeField] AudioSource lvlUpSFX;

    [Header("Combat (à remplir plus tard)")]
    [SerializeField] AudioSource[] blockSFX;
    [SerializeField] AudioSource[] parrySFX;
    [SerializeField] AudioSource[] dodgeSFX;
    [SerializeField] AudioSource[] playerHurtSFX;
    [SerializeField] AudioSource[] enemyDeathSFX;


    public void PlayThemeSFX() {
        this.randomTheme = GameController.Random(0, this.mainTheme.Length - 1);
        this.mainTheme[this.randomTheme].Play();
        Invoke(nameof(this.PlayThemeSFX), this.mainTheme[this.randomTheme].clip.length);
    }

    public void PlayMenuSFX() {
        this.randomMenuSFX = GameController.Random(0, this.menuSFX.Length - 1);
        this.menuSFX[this.randomMenuSFX].Play();
        Invoke(nameof(this.PlayMenuSFX), this.menuSFX[this.randomMenuSFX].clip.length);
    }

    public void PlayBossThemeSFX(int music) {
        this.randomBossTheme = music;
        this.bossTheme[this.randomBossTheme].Play();
        Invoke(nameof(this.PlayBossThemeSFX), this.bossTheme[this.randomBossTheme].clip.length);
    }

    public void PlaySlashSFX() {
        this.PlaySlashSFX(1f);
    }

    // Les 34 armes partagent 3 sons. Moduler la hauteur suffit à les distinguer à
    // l'oreille — grave et lourd pour une masse, sec et haut pour une dague — sans
    // avoir à produire un seul nouvel échantillon.
    public void PlaySlashSFX(float pitch) {

        if(this.slashSFX == null || this.slashSFX.Length == 0) return;

        AudioSource source = this.slashSFX[GameController.Random(0, this.slashSFX.Length - 1)];
        if(source == null) return;

        source.pitch = Mathf.Clamp(pitch + UnityEngine.Random.Range(-0.06f, 0.06f), 0.5f, 2f);
        source.Play();
    }

    // Points d'entrée du combat. Les tableaux sont vides pour l'instant : le projet
    // n'a que 3 sons de combat. Chaque méthode se contente de ne rien faire tant que
    // les clips ne sont pas renseignés dans l'inspecteur.
    public void PlayBlockSFX()       { PlayRandom(this.blockSFX,      0.9f); }
    public void PlayParrySFX()       { PlayRandom(this.parrySFX,      1.15f); }
    public void PlayDodgeSFX()       { PlayRandom(this.dodgeSFX,      1f); }
    public void PlayPlayerHurtSFX()  { PlayRandom(this.playerHurtSFX, 1f); }
    public void PlayEnemyDeathSFX()  { PlayRandom(this.enemyDeathSFX, 1f); }

    private static void PlayRandom(AudioSource[] sources, float pitch) {

        if(sources == null || sources.Length == 0) return;

        AudioSource source = sources[GameController.Random(0, sources.Length - 1)];
        if(source == null) return;

        source.pitch = Mathf.Clamp(pitch + UnityEngine.Random.Range(-0.05f, 0.05f), 0.5f, 2f);
        source.Play();
    }

    public void PlayDeathSFX() { 
        this.deathSFX.Play(); 
    }

    public void PlayPauseMenuSFX() { 
        this.pauseMenuSFX.Play(); 
    }

    public void PlayLevelUpSFX() { 
        this.lvlUpSFX.Play(); 
    }

    public void StopPauseMenuSFX() { 
        this.pauseMenuSFX.Stop(); 
    }
    
    public void StopThemeSFX() { 
        this.mainTheme[this.randomTheme].Stop();
        CancelInvoke(nameof(this.PlayThemeSFX));
    }

    public void StopMenuSFX() { 
        this.menuSFX[this.randomMenuSFX].Stop();
        CancelInvoke(nameof(this.PlayMenuSFX));
    }

    public void StopBossThemeSFX() { 
        this.bossTheme[this.randomBossTheme].Stop();
        CancelInvoke(nameof(this.PlayBossThemeSFX));
    }

    public void PlayPickUpSFX() {
        this.pickUpSFX.Play();
    }
}
