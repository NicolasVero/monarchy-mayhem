using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WeaponsDropper : MonoBehaviour {

    [SerializeField] private GameObject[] models;
    [SerializeField] private GameObject weaponsContainer;
    private float[] probabilities;

    void Start() {
        this.probabilities = this.BuildProbabilitiesArray();
    }

    private float[] BuildProbabilitiesArray() {
        
        List<float> probs = new List<float>();
        TextAsset weaponsList = Resources.Load<TextAsset>("Data/WeaponsStats");

        if (weaponsList != null) {

            WeaponsStats weaponsStats = JsonUtility.FromJson<WeaponsStats>(weaponsList.text);

            foreach (WeaponStat weapon in weaponsStats.weapons)
                probs.Add(weapon.dropProbability);
        }

        return probs.ToArray();
    }


    public void CreateWeapon(int weaponID, Vector3 position) {

        position.y += (position.y > 1.5f) ? 0f : 1f;
        weaponID = (weaponID == -1) ? GiveRandomWeaponID() : weaponID;

        if (weaponID >= 0 && weaponID < models.Length) {
            var instance = Instantiate(models[weaponID], position, Quaternion.Euler(0f, 0f, 90f));
            instance.transform.parent = weaponsContainer.transform;
        }
    }

    public void CreateWeapon(Vector3 position) {
        CreateWeapon(-1, position);
    }

    // Rejection sampling : la boucle était infinie par construction. Elle se termine
    // en pratique, mais un tableau vide ou des probabilités toutes à zéro figeaient
    // le jeu. On borne les tirages et on retombe sur un tirage uniforme.
    private int GiveRandomWeaponID() {

        if (this.probabilities == null || this.probabilities.Length == 0)
            return -1;

        const int maxAttempts = 200;

        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            int number = Random.Range(0, this.probabilities.Length);

            if (Random.value < this.probabilities[number])
                return number;
        }

        return Random.Range(0, this.probabilities.Length);
    }

    public int GetWeaponsListLength() {
        return models.Length;
    }
}