using UnityEngine;

// Instancié automatiquement par TimeController.Bootstrap() : rien à câbler dans les scènes.
public class TimeControllerRunner : MonoBehaviour {

    private void Update() {
        TimeController.Tick();
    }
}
