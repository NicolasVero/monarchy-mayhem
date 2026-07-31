using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour {

    private Camera playerCamera = FindObjectOfType<Camera>();
    private static bool canInteract = true;

    public static int Random(int min, int max) {
        return UnityEngine.Random.Range(min, max + 1);
    }

    public static float RandomFloat() {
        return UnityEngine.Random.value;
    }

    // Le timeScale appartient à TimeController : il arbitre entre la pause,
    // le ralenti de mort et le hitstop du combat, qui écrivaient sinon tous
    // sur Time.timeScale sans se voir.
    public static void SetGameState() {
        TimeController.TogglePause();
    }

    public static void SetGameState(bool state) {
        TimeController.SetBaseScale(state ? 1f : 0f);
    }

    public static void SetGameState(float value) {
        TimeController.SetBaseScale(value);
    }

    // Doit rester faux pendant un hitstop, sinon l'input d'attaque est bloqué
    // à chaque coup porté (PlayerController vérifie GameIsFreeze()).
    public static bool GameIsFreeze() {
        return TimeController.IsPaused;
    }

    public static void SetCursorVisibility(bool state) {
        Cursor.visible = state;
        Cursor.lockState = (state) ? CursorLockMode.Confined : CursorLockMode.Locked;
    }

    public static void SetPanelVisibility(GameObject panel, bool state) {
        panel.SetActive(state);
    }

    public static void SetCanvasVisibility(Canvas canvas, bool state) {
        canvas.enabled = state;
    }

    public static void SetCanvasVisibility(Canvas[] canvas, bool state) {
        foreach(Canvas canva in canvas)
            canva.enabled = state;
    }

    public static void DrawCircleAroundObject(Vector3 position, float range, int numRays = 36) {
        float angleIncrement = 360.0f / numRays;
        Color rayColor = Color.red;

        for (int i = 0; i < numRays; i++) {
            float angle = i * angleIncrement;
            float x = Mathf.Cos(Mathf.Deg2Rad * angle) * range;
            float z = Mathf.Sin(Mathf.Deg2Rad * angle) * range;

            Vector3 rayDirection = new Vector3(x, 0.0f, z);
            Debug.DrawRay(position, rayDirection, rayColor);
        }
    }

    public static void SetCanInteract(bool state) {
        canInteract = state;
    }
    
    public static void SetCanInteract() {
        canInteract = false;
    }

    
    public static bool GetCanInteract() {
        return canInteract;
    }

    public static void DestroyWeapon(Weapon weapon) {
        var renderer = weapon.GetComponent<Renderer>();
        var collider = weapon.GetComponent<Collider>();

        if(renderer != null) Destroy(renderer);
        if(collider != null) Destroy(collider);

        Destroy(weapon);
    }


    public static void ShowPauseMenu(GameObject pauseMenu) {
        if(pauseMenu == null) return;
        
        pauseMenu.SetActive(true);
        GameController.SetCursorVisibility(true);
    }
    

    public static void HidePauseMenu(GameObject pauseMenu) {
        if(pauseMenu == null) return;

        pauseMenu.SetActive(false);
        GameController.SetCursorVisibility(false);
    }

    public static void SetMenuAlpha(GameObject gameObject, float alpha) {
    
        CanvasRenderer[] renderers = gameObject.GetComponentsInChildren<CanvasRenderer>();

        foreach (CanvasRenderer renderer in renderers) {
            renderer.SetAlpha(alpha);
        }
    }

    public static float GetGameObjectAlpha(GameObject gameObject) {
        CanvasRenderer[] renderers = gameObject.GetComponentsInChildren<CanvasRenderer>();

        if(renderers.Length > 0) 
            return renderers[0].GetAlpha();
        
        return -1f;
    }

    public static string GetSystemLanguageUpper() {
        return (Application.systemLanguage == SystemLanguage.French) ? "FR" : "EN";
    }

    public static string GetSystemLanguageLower() {
        return (Application.systemLanguage == SystemLanguage.French) ? "fr" : "en";
    }
}
