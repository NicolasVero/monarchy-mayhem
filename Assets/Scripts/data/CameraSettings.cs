using System;

// Réglages de la caméra de combat, sortis en JSON pour pouvoir être ajustés
// sans recompiler : le cadrage se règle à l'œil, pas au raisonnement.
[Serializable]
public class CameraSettings {

    public float distance;        // recul derrière le joueur
    public float height;          // hauteur du point de pivot, au-dessus des pieds
    public float shoulder;        // décalage latéral ; positif = caméra à droite

    public float sensitivity;     // degrés par unité de déplacement souris
    public float pitchMin;        // regarder vers le bas (négatif)
    public float pitchMax;        // regarder vers le haut

    public float smooth;          // temps de lissage du suivi, en secondes
    public float collisionRadius; // rayon du bras télescopique
}
