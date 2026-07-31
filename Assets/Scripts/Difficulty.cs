using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Difficulty : MonoBehaviour {

	[SerializeField] private Canvas choice;
	[SerializeField] private StartMenuController menu;
	private string difficulty;

	void Start() {
		this.DisableChoice();
	}

	public void ChooseDifficulty(string choice) {
		this.difficulty = choice;
		SceneManager.LoadScene(Names.Scenes[0]);
	}

	// Difficulté appliquée quand aucun objet Difficulty n'est présent — c'est le cas
	// dès qu'on lance une scène de jeu directement depuis l'éditeur, sans passer par
	// le menu. Mettre "easy" ici rend les tests confortables ; repasser à "medium"
	// avant une build si le comportement par défaut doit changer.
	public const string Default = "easy";

	public string GetDifficulty() {
		return (this.difficulty == null) ? Default : this.difficulty;
	}

	public void EnableChoice() {
		this.choice.enabled = true;
	}

	public void DisableChoice() {
		this.choice.enabled = false;
	}
}
