using System;
using UnityEngine;

// Réserve d'endurance du joueur.
//
// Sans elle, attaquer et sprinter étaient gratuits et illimités : on pouvait courir
// en arrière indéfiniment en frappant. L'endurance met un prix sur chaque action
// offensive et sur la fuite, et c'est elle qui alimente la roulade.
//
// Le maximum et la régénération possèdent chacun cinq améliorations dédiées.
public class Stamina : MonoBehaviour {

    private float max = 100f;
    private float current = 100f;
    private float regenPerSecond = 22f;
    private float regenDelay = 0.8f;
    private float lastSpendAt = -99f;

    // Sous ce seuil après épuisement, on ne peut rien relancer : évite de repartir
    // à l'attaque avec 2 points d'endurance dès la première frame de régénération.
    private const float EXHAUSTED_RECOVERY = 15f;
    private bool exhausted;

    public event Action Changed;

    public float Max { get { return this.max; } }
    public float Current { get { return this.current; } }
    public float RegenPerSecond { get { return this.regenPerSecond; } }
    public float Normalized { get { return (this.max > 0f) ? this.current / this.max : 0f; } }
    public bool IsExhausted { get { return this.exhausted; } }

    public void Configure(float maxStamina, float regen, float delay) {
        this.max = Mathf.Max(maxStamina, 1f);
        this.regenPerSecond = Mathf.Max(regen, 0f);
        this.regenDelay = Mathf.Max(delay, 0f);
        this.current = this.max;
        this.exhausted = false;

        this.Raise();
    }

    // Augmenter la capacité conserve l'endurance actuelle et accorde le nouveau gain.
    public void SetMax(float maxStamina) {
        float previous = this.max;
        this.max = Mathf.Max(maxStamina, 1f);
        this.current = Mathf.Min(this.current + Mathf.Max(this.max - previous, 0f), this.max);

        this.Raise();
    }

    public void SetRegen(float regen) {
        this.regenPerSecond = Mathf.Max(regen, 0f);
    }

    public bool Has(float amount) {
        return !this.exhausted && this.current >= amount;
    }

    // Retourne faux et ne consomme rien si la réserve est insuffisante.
    public bool TrySpend(float amount) {
        if(amount <= 0f) return true;
        if(!this.Has(amount)) return false;

        this.current -= amount;
        this.lastSpendAt = Time.time;

        if(this.current <= 0.01f) {
            this.current = 0f;
            this.exhausted = true;
        }

        this.Raise();
        return true;
    }

    // Vide la réserve d'un coup : garde brisée.
    public void Empty() {
        this.current = 0f;
        this.exhausted = true;
        this.lastSpendAt = Time.time;

        this.Raise();
    }

    // Dépense continue (sprint, garde) : consomme ce qui est disponible et signale
    // si la réserve a tenu.
    public bool Drain(float amountPerSecond) {
        if(this.exhausted) return false;

        float amount = amountPerSecond * Time.deltaTime;
        if(amount <= 0f) return true;

        this.current -= amount;
        this.lastSpendAt = Time.time;

        if(this.current <= 0f) {
            this.current = 0f;
            this.exhausted = true;
            this.Raise();
            return false;
        }

        this.Raise();
        return true;
    }

    private void Update() {
        if(this.current >= this.max) return;
        if(Time.time - this.lastSpendAt < this.regenDelay) return;

        this.current = Mathf.Min(this.current + this.regenPerSecond * Time.deltaTime, this.max);

        if(this.exhausted && this.current >= EXHAUSTED_RECOVERY)
            this.exhausted = false;

        this.Raise();
    }

    private void Raise() {
        if(this.Changed != null) this.Changed();
    }
}
