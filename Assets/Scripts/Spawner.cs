using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Spawner : MonoBehaviour {


    private bool allowPeasants;
    private bool allowBourgeois;
    private bool allowKnights;

    private int chancePeasants;
    private int chanceBourgeois;
    private int chanceKnights;

    private bool viewSpawnPoint;
    private float spawnDelay;

    private GameObject[] peasants;
    private GameObject[] bourgeois;
    private GameObject[] knights;
    private GameObject[] pickUps;


    private GameObject enemiesContainer;
    private GameObject collectiblesContainer;
    private bool isActive = false, isPaused = false, allowPickUp;
    private float timer = 0f;
    private int currentPickUpIndex = 0;
    private GameObject[] currentPickUpGroup;
    private Difficulty difficultyController;
    private SpawnersController spawnerController;
    private float radius;
    private PlayerController player;
    private bool hasEncounterPool;
    private string encounterScene;


    void Awake(){

        GameObject spawn = transform.Find("Spawn").gameObject;
        spawn.SetActive(viewSpawnPoint);


        this.difficultyController = FindObjectOfType<Difficulty>();

        if(this.difficultyController != null) {

            this.difficultyController.DisableChoice();

            if(this.difficultyController.GetDifficulty() == "easy") this.spawnDelay = 5;
            if(this.difficultyController.GetDifficulty() == "medium") this.spawnDelay = 4;
            if(this.difficultyController.GetDifficulty() == "hard") this.spawnDelay = 2;
        } else {
            this.spawnDelay = 4;
        }
    }

    void Start() {
        this.player = GameObject.FindWithTag(Names.MainCharacter).GetComponent<PlayerController>();
        this.spawnerController = GetComponentInParent<SpawnersController>();
        this.radius = this.spawnerController.GetRadius();

        this.allowPeasants = this.spawnerController.GetAllowPeasant();
        this.allowBourgeois = this.spawnerController.GetAllowBouregois();
        this.allowKnights = this.spawnerController.GetAllowKnights();

        this.chancePeasants = this.spawnerController.GetChancePeasants();
        this.chanceBourgeois = this.spawnerController.GetChanceBourgeois();
        this.chanceKnights = this.spawnerController.GetChanceKnights();
    
        this.enemiesContainer = this.spawnerController.GetEnemiesContainer();
        this.collectiblesContainer = this.spawnerController.GetColleciblesContainer();
    
        this.viewSpawnPoint = this.spawnerController.GetViewSpawnPoint();

        // Awake() a calculé un délai selon la difficulté (5 / 4 / 2). La valeur sérialisée
        // du contrôleur l'écrasait systématiquement, or elle vaut 0 sur Village et Chateau
        // et 1 sur Tutorial : les spawners tiraient à chaque frame — ou presque — jusqu'au
        // plafond d'entités, et le réglage par difficulté était du code mort.
        // La difficulté fait maintenant référence ; une scène peut seulement ralentir.
        this.spawnDelay = Mathf.Max(this.spawnDelay, this.spawnerController.GetSpawnDelay());
    
        this.peasants = this.spawnerController.GetPeasantsPrefabs();
        this.bourgeois = this.spawnerController.GetBourgeoisPrefabs();
        this.knights = this.spawnerController.GetKnightsPrefabs();
        this.pickUps = this.spawnerController.GetPickUpsPrefabs();

        this.encounterScene = SceneManager.GetActiveScene().name;
        this.hasEncounterPool = this.spawnerController.UsesEncounterPool(this.encounterScene);

        if(this.currentPickUpGroup == null || this.currentPickUpGroup.Length == 0) {
            this.GeneratePickUpGroup();
        }
    }

    void Update() {

        if(this.isActive && !this.isPaused && !this.IsPlayerInRadius()) {
            this.timer += Time.deltaTime;

            if(this.timer >= this.spawnDelay) {
                this.timer = 0f;
                this.SpawnEnemies();
            }
        }
    }
    
    private void SpawnEnemies() {

        if(this.hasEncounterPool) {
            GameObject selected = this.spawnerController.PickEncounterEnemy(this.encounterScene);
            if(selected != null) this.SpawnEnemy(selected);
            return;
        }

        // Repli pour une scene non declaree : les anciens pourcentages deviennent
        // des poids, ce qui garantit toujours au plus un spawn par intervalle.
        int peasantWeight = this.allowPeasants && this.peasants != null && this.peasants.Length > 0
                          ? Mathf.Max(this.chancePeasants, 0)
                          : 0;
        int bourgeoisWeight = this.allowBourgeois && this.bourgeois != null && this.bourgeois.Length > 0
                            ? Mathf.Max(this.chanceBourgeois, 0)
                            : 0;
        int knightWeight = this.allowKnights && this.knights != null && this.knights.Length > 0
                         ? Mathf.Max(this.chanceKnights, 0)
                         : 0;
        int total = peasantWeight + bourgeoisWeight + knightWeight;

        if(total <= 0) return;

        int roll = GameController.Random(0, total - 1);

        if(roll < peasantWeight) {
            this.SpawnEnemy(this.peasants[GameController.Random(0, this.peasants.Length - 1)]);
        } else if(roll < peasantWeight + bourgeoisWeight) {
            this.SpawnEnemy(this.bourgeois[GameController.Random(0, this.bourgeois.Length - 1)]);
        } else {
            this.SpawnEnemy(this.knights[GameController.Random(0, this.knights.Length - 1)]);
        }
    }

    private void SpawnEnemy(GameObject prefab) {
        if(prefab == null) return;

        Transform spawned = Instantiate(prefab, transform.position, Quaternion.identity).transform;
        spawned.SetParent(this.enemiesContainer.transform);
    }

    private void SpawnPickUp() {
        if(this.currentPickUpIndex >= this.currentPickUpGroup.Length) {
            this.currentPickUpIndex = 0;
        }

        GameObject pickupToSpawn = this.currentPickUpGroup[this.currentPickUpIndex];
        Vector3 spawnPosition = transform.position + Vector3.up * 1.0f;
        Instantiate(pickupToSpawn, spawnPosition, Quaternion.identity).transform.parent = this.collectiblesContainer.transform;
    }



    public void ActiveSpawnerPickUp() {
        this.allowPickUp = true;
        SpawnPickUp();
    }

    private void GeneratePickUpGroup() {
        this.currentPickUpGroup = new GameObject[this.pickUps.Length];
        for(int i = 0; i < this.currentPickUpGroup.Length; i++) 
            this.currentPickUpGroup[i] = this.pickUps[i % this.pickUps.Length];
    }

    public void ActiveSpawner() {
        this.isActive = true;
    }

    public void PauseSpawner() {
        this.isPaused = true;
    }

    public void ResumeSpawner() {
        this.isPaused = false;
    }

    public void IncrementIndex(){
        this.currentPickUpIndex++;
    }

    public bool IsPlayerInRadius() {
        return Vector3.Distance(this.player.transform.position, transform.position) <= this.radius;
    }
}
