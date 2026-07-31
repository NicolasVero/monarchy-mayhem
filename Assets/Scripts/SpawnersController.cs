using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnersController : MonoBehaviour {
    
    [Header("Spawners")]
    [SerializeField] private int maxActive;
    [SerializeField] private int maxEntities;
    [SerializeField] private float radius;

    [Header("Containers")]
    [SerializeField] private GameObject enemiesContainer;
    [SerializeField] private GameObject collectiblesContainer;
    
    [Header("Spawnable")]
    [SerializeField] bool allowPeasants;
    [SerializeField] bool allowBourgeois;
    [SerializeField] bool allowKnights;

    [Header("Chance of spawn")]
    [Range(0, 100)][SerializeField] int chancePeasants;
    [Range(0, 100)][SerializeField] int chanceBourgeois;
    [Range(0, 100)][SerializeField] int chanceKnights;

    [Header("Spawn parameters")]
    [SerializeField] private bool viewSpawnPoint;
    [SerializeField] private float spawnDelay;

    [Header("Combat testing")]
    [SerializeField] private bool specialEnemyTestMode;

    [Header("Instances")]
    [SerializeField] private GameObject[] peasants;
    [SerializeField] private GameObject[] bourgeois;
    [SerializeField] private GameObject[] knights;
    [SerializeField] private GameObject[] pickUps;


    private bool isPaused = false;
    private GameObject[] spawnerObjects;
    private Difficulty difficultyController;
    private int specialEnemyIndex;

    void Start() {

        this.difficultyController = FindObjectOfType<Difficulty>();

        if(this.difficultyController != null) {

            this.difficultyController.DisableChoice();

            // Plafonds abaissés depuis la refonte du combat : les ennemis étaient plus
            // lents que le joueur et la moitié de la horde n'arrivait jamais jusqu'à
            // lui. Maintenant qu'ils convergent tous et qu'ils télégraphient leurs
            // coups, 20 simultanés rendaient le combat illisible.
            if(this.difficultyController.GetDifficulty() == "easy") this.maxEntities = 5;
            if(this.difficultyController.GetDifficulty() == "medium") this.maxEntities = 9;
            if(this.difficultyController.GetDifficulty() == "hard") this.maxEntities = 16;
        } else {
            this.maxEntities = 5;   // pas de menu : on lance une scène en test, cf. Difficulty.Default
        }
        
    
        List<GameObject> spawnerList = new List<GameObject>();
        foreach(Transform child in transform) {
            if(child.GetComponent<Spawner>() != null) {
                spawnerList.Add(child.gameObject);
            }
        }

        this.spawnerObjects = spawnerList.ToArray();

        if(this.maxActive > this.spawnerObjects.Length)
            this.maxActive = this.spawnerObjects.Length;

        this.ActivateRandomSpawners();
    }

    void Update() {

        if(this.enemiesContainer.transform.childCount >= maxEntities) {
            if(!this.isPaused) 
                this.PauseSpawners();
        } else {
            if(this.isPaused) 
                this.ResumeSpawners();
        }
    }

    private void ActivateRandomSpawners() {
        List<int> indices = new List<int>();

        for(int i = 0; i < this.spawnerObjects.Length; i++) {
            indices.Add(i);
        }

        for(int i = 0; i < maxActive; i++) {
            int randomIndex = GameController.Random(0, indices.Count - 1);
            int spawnerIndex = indices[randomIndex];
            this.spawnerObjects[spawnerIndex].GetComponent<Spawner>().ActiveSpawner();
            indices.RemoveAt(randomIndex);
        }
    }

    public void PauseSpawners() {
        foreach(GameObject spawnerObject in this.spawnerObjects) {
            spawnerObject.GetComponent<Spawner>().PauseSpawner();
        }
        this.isPaused = true;
    }

    public void ResumeSpawners() {
        foreach(GameObject spawnerObject in this.spawnerObjects) {
            spawnerObject.GetComponent<Spawner>().ResumeSpawner();
        }
        this.isPaused = false;
    }

    public void SetMaxEntities(int maxEntities) {
        this.maxEntities = maxEntities;
    } 

    public bool UsesEncounterPool(string sceneName) {
        return this.specialEnemyTestMode || EncounterPoolLibrary.HasPool(sceneName);
    }

    public GameObject PickEncounterEnemy(string sceneName) {
        if(!this.specialEnemyTestMode)
            return EncounterPoolLibrary.Pick(sceneName);

        GameObject selected = EncounterPoolLibrary.PickSpecial(sceneName, this.specialEnemyIndex);
        this.specialEnemyIndex++;

        return selected != null ? selected : EncounterPoolLibrary.Pick(sceneName);
    }

    public float GetRadius() {
        return this.radius;
    }

    public bool GetAllowPeasant() {
        return this.allowPeasants;
    }

    public bool GetAllowBouregois() {
        return this.allowBourgeois;
    }

    public bool GetAllowKnights() {
        return this.allowKnights;
    }

    public int GetChancePeasants() {
        return this.chancePeasants;
    }

    public int GetChanceBourgeois() {
        return this.chanceBourgeois;
    }

    public int GetChanceKnights() {
        return this.chanceKnights;
    }

    public GameObject GetColleciblesContainer() {
        return this.collectiblesContainer;
    }
    
    public GameObject GetEnemiesContainer() {
        return this.enemiesContainer;
    }

    public bool GetViewSpawnPoint() {
        return this.viewSpawnPoint;
    }

    public float GetSpawnDelay() {
        return this.spawnDelay;
    }

    public GameObject[] GetPeasantsPrefabs() {
        return this.peasants;
    }

    public GameObject[] GetBourgeoisPrefabs() {
        return this.bourgeois;
    }

    public GameObject[] GetKnightsPrefabs() {
        return this.knights;
    }

    public GameObject[] GetPickUpsPrefabs() {
        return this.pickUps;
    }
}
