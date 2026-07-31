using UnityEngine;

// Résistance à l'interruption.
//
// Rien n'interrompait un ennemi : frappé en pleine anticipation, il touchait quand
// même. Avec la poise, les coups accumulent une pression qui finit par le briser —
// vite avec une masse, presque jamais avec une dague sur un chevalier. C'est ce qui
// donne un sens au choix de l'arme.
//
// Classe simple (pas un MonoBehaviour) : elle est portée par les contrôleurs
// existants, qui gardent la main sur leur cycle de vie.
public class Poise {

    private readonly float max;
    private readonly float decayPerSecond;
    private float current;

    public float Normalized { get { return (this.max > 0f) ? this.current / this.max : 0f; } }

    public Poise(float maxPoise, float decayDuration) {
        this.max = Mathf.Max(maxPoise, 1f);
        this.decayPerSecond = this.max / Mathf.Max(decayDuration, 0.1f);
        this.current = 0f;
    }

    // Vrai quand le seuil est franchi : l'ennemi part en stagger et la jauge repart de zéro.
    public bool Accumulate(float amount) {

        if(amount <= 0f) return false;

        this.current += amount;

        if(this.current >= this.max) {
            this.current = 0f;
            return true;
        }

        return false;
    }

    // La pression retombe si on cesse de frapper : enchaîner devient nécessaire.
    public void Decay(float deltaTime) {
        if(this.current <= 0f) return;

        this.current = Mathf.Max(this.current - this.decayPerSecond * deltaTime, 0f);
    }

    public void Reset() {
        this.current = 0f;
    }
}
