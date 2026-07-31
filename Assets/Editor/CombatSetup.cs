using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Outillage de la refonte du combat.
//
// Le pack Animations_Starter_Pack (Assets/Resources/NPC/Blink/...) contient exactement
// les animations qui manquaient — roulades, encaissement, garde, attaques 1H/2H — et
// n'était référencé nulle part. Tous les rigs du projet sont en Humanoid (King.fbx a
// un mapping explicite de 40 os), donc Unity retargette ces clips sans conversion.
//
// Ce script ajoute les états correspondants aux Animator Controllers. Les transitions
// ne sont volontairement pas créées : le code pilote tout par CrossFade.
//
// Menu : Tools/Monarchy/Combat/...
public static class CombatSetup {

    private const string PLAYER_CONTROLLER = "Assets/Resources/Animations/AnimationControllers/KingMovement.controller";
    private const string ENEMY_CONTROLLER  = "Assets/Resources/Animations/AnimationControllers/EnemyController.controller";
    private const string BOSS_CONTROLLER   = "Assets/Resources/Animations/AnimationControllers/BossMovementController.controller";

    private const string COMBAT_PATH   = "Assets/Resources/NPC/Blink/Art/Animations/Animations_Starter_Pack/Combat/";
    private const string MOVEMENT_PATH = "Assets/Resources/NPC/Blink/Art/Animations/Animations_Starter_Pack/Movement/";
    private const string EXTERNAL_COMBAT_PATH = "Assets/Resources/Animations/Combat/";

    private const string ATTACK_SPEED_PARAM = "AttackSpeed";
    private const string OFFICIAL_POLEARM_CLIP = "HumanM@AttackPolearm01";


    // États ajoutés par cet outil, pour pouvoir les retirer proprement.
    private static readonly string[] PLAYER_STATES = {
        "Roll_Forward", "Roll_Back", "Roll_Left", "Roll_Right",
        "MeleeAttack_OneHanded", "MeleeAttack_TwoHanded",
        "HeavyCombo_Right", "HeavyCombo_Left",
        "DaggerCombo_1", "DaggerCombo_2", "DaggerCombo_3", "DaggerCombo_4",
        "Punch_Left", "Punch_Right", "Block", "Stunned",
        "SwordCombo_1", "SwordCombo_2", "SwordCombo_3",
        "PolearmThrust_1", "PolearmThrust_2", "PolearmThrust_3",
        "OverhandThrow"
    };

    private static readonly string[] ENEMY_STATES = {
        "GetHit", "Stunned", "Stagger",
        "PolearmThrust_1", "OverhandThrow", "Buff"
    };


    // Les roles tactiques sont ajoutes au runtime : leurs etats doivent donc exister
    // sans demander une operation manuelle dans le menu de l'editeur.
    [InitializeOnLoadMethod]
    private static void EnsureCombatStatesOnReload() {
        EditorApplication.delayCall += () => {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;

            int added = SetupPlayer()
                      + SetupEnemy(ENEMY_CONTROLLER)
                      + SetupEnemy(BOSS_CONTROLLER);

            if(added <= 0) return;

            AssetDatabase.SaveAssets();
            Debug.Log("[CombatSetup] " + added + " etat(s) requis ajoutes ou actualises automatiquement.");
        };
    }

    internal static void RefreshPolearmStates() {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;

        int changed = SetupPlayer()
                    + SetupEnemy(ENEMY_CONTROLLER)
                    + SetupEnemy(BOSS_CONTROLLER);
        if(changed <= 0) return;

        AssetDatabase.SaveAssets();
        Debug.Log("[CombatSetup] Animation de polearm officielle appliquee au joueur et aux ennemis.");
    }


    // Le roi est le rig le plus sûr : son avatar Humanoid a un mapping explicite de
    // 40 os, donc les clips du pack Blink s'y retargettent proprement.
    [MenuItem("Tools/Monarchy/Combat/Ajouter les états d'animation (joueur)")]
    public static void AddAnimatorStates() {

        int added = SetupPlayer();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CombatSetup] Joueur : " + added + " état(s) ajouté(s).");
    }


    // Séparé volontairement : les ennemis sont des personnages Polytope, dont le
    // squelette diffère de celui du pack Blink. Le retargeting Humanoid devrait
    // fonctionner, mais une déformation du maillage se verrait ici en premier.
    // Le stagger reste pleinement fonctionnel sans ces états — il perd seulement
    // son animation d'encaissement.
    [MenuItem("Tools/Monarchy/Combat/Ajouter les réactions ennemies (expérimental)")]
    public static void AddEnemyReactions() {

        int added = SetupEnemy(ENEMY_CONTROLLER) + SetupEnemy(BOSS_CONTROLLER);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CombatSetup] Ennemis : " + added + " état(s) ajouté(s).");
    }


    [MenuItem("Tools/Monarchy/Combat/Retirer tous les états ajoutés")]
    public static void RemoveAnimatorStates() {

        int removed = 0;

        removed += RemoveStates(PLAYER_CONTROLLER, PLAYER_STATES);
        removed += RemoveStates(ENEMY_CONTROLLER, ENEMY_STATES);
        removed += RemoveStates(BOSS_CONTROLLER, ENEMY_STATES);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CombatSetup] " + removed + " état(s) retiré(s). Le combat reste jouable, sans les animations correspondantes.");
    }


    private static int RemoveStates(string controllerPath, string[] stateNames) {

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller == null) return 0;

        int removed = 0;

        foreach(AnimatorControllerLayer layer in controller.layers) {

            AnimatorStateMachine machine = layer.stateMachine;
            if(machine == null) continue;

            // Copie : RemoveState modifie la collection pendant l'itération.
            ChildAnimatorState[] states = machine.states;

            foreach(ChildAnimatorState child in states) {

                if(child.state == null) continue;
                if(System.Array.IndexOf(stateNames, child.state.name) < 0) continue;

                // Ne jamais retirer l'état par défaut : la couche deviendrait invalide.
                if(machine.defaultState == child.state) {
                    Debug.LogWarning("[CombatSetup] \"" + child.state.name + "\" est l'état par défaut de la couche " + layer.name + " : conservé.");
                    continue;
                }

                machine.RemoveState(child.state);
                removed++;
            }
        }

        return removed;
    }


    private static int SetupPlayer() {

        AnimatorController controller = Load(PLAYER_CONTROLLER);
        if(controller == null) return 0;

        EnsureFloatParameter(controller, ATTACK_SPEED_PARAM, 1f);

        AnimatorStateMachine movement  = GetLayer(controller, "Movement Layer", 0);
        AnimatorStateMachine upperBody = GetLayer(controller, "UpperBody Layer", 1);

        int added = 0;

        // Roulades : corps entier, donc sur la couche de déplacement.
        // La vitesse est relevée pour coller aux 0.65 s de la roulade côté code.
        added += AddState(movement, "Roll_Forward", MOVEMENT_PATH + "RollForward.fbx", 1.4f, null);
        added += AddState(movement, "Roll_Back",    MOVEMENT_PATH + "RollBackward.fbx", 1.4f, null);
        added += AddState(movement, "Roll_Left",    MOVEMENT_PATH + "RollLeft.fbx", 1.4f, null);
        added += AddState(movement, "Roll_Right",   MOVEMENT_PATH + "RollRight.fbx", 1.4f, null);

        // Attaques et garde : haut du corps, pour rester mobile pendant le coup.
        // Leur vitesse est pilotée par le paramètre AttackSpeed, comme l'état Attack.
        added += AddState(upperBody, "MeleeAttack_OneHanded", COMBAT_PATH + "MeleeAttack_OneHanded.fbx", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "MeleeAttack_TwoHanded", COMBAT_PATH + "MeleeAttack_TwoHanded.fbx", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "HeavyCombo_Right",      COMBAT_PATH + "MeleeAttack_TwoHanded.fbx", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "HeavyCombo_Left",       COMBAT_PATH + "MeleeAttack_TwoHanded.fbx", 1f, ATTACK_SPEED_PARAM, true);
        added += AddState(upperBody, "DaggerCombo_1",          COMBAT_PATH + "MeleeAttack_OneHanded.fbx", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "DaggerCombo_2",          COMBAT_PATH + "MeleeAttack_OneHanded.fbx", 1f, ATTACK_SPEED_PARAM, true);
        added += AddState(upperBody, "DaggerCombo_3",          EXTERNAL_COMBAT_PATH + "SwordCombo_1.anim", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "DaggerCombo_4",          EXTERNAL_COMBAT_PATH + "SwordCombo_2.anim", 1f, ATTACK_SPEED_PARAM, true);
        added += AddState(upperBody, "Punch_Left",            COMBAT_PATH + "PunchLeft.fbx", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "Punch_Right",           COMBAT_PATH + "PunchRight.fbx", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "SwordCombo_1",           EXTERNAL_COMBAT_PATH + "SwordCombo_1.anim", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "SwordCombo_2",           EXTERNAL_COMBAT_PATH + "SwordCombo_2.anim", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "SwordCombo_3",           EXTERNAL_COMBAT_PATH + "SwordCombo_3.anim", 1f, ATTACK_SPEED_PARAM);
        added += EnsurePlayerPolearmStates(upperBody);
        added += AddState(upperBody, "OverhandThrow",          EXTERNAL_COMBAT_PATH + "OverhandThrow.anim", 1f, ATTACK_SPEED_PARAM);
        added += AddState(upperBody, "Block",                 COMBAT_PATH + "BlockingLoop.fbx", 1f, null);
        added += AddState(upperBody, "Stunned",               COMBAT_PATH + "StunnedLoop.fbx", 1f, null);

        return added;
    }

    private static int EnsurePlayerPolearmStates(AnimatorStateMachine machine) {
        AnimationClip officialClip = FindOfficialPolearmClip();

        AnimationClip first = officialClip != null
            ? officialClip
            : LoadClip(EXTERNAL_COMBAT_PATH + "PolearmThrust_1.anim");
        AnimationClip second = officialClip != null
            ? officialClip
            : LoadClip(EXTERNAL_COMBAT_PATH + "PolearmThrust_2.anim");
        AnimationClip third = officialClip != null
            ? officialClip
            : LoadClip(EXTERNAL_COMBAT_PATH + "PolearmThrust_3.anim");

        int changed = 0;
        changed += EnsureStateMotion(machine, "PolearmThrust_1", first, 1f, ATTACK_SPEED_PARAM);
        changed += EnsureStateMotion(machine, "PolearmThrust_2", second, 1f, ATTACK_SPEED_PARAM, officialClip != null);
        changed += EnsureStateMotion(machine, "PolearmThrust_3", third, 1f, ATTACK_SPEED_PARAM);
        return changed;
    }


    private static int SetupEnemy(string controllerPath) {

        AnimatorController controller = Load(controllerPath);
        if(controller == null) return 0;

        // Permet d'étirer l'animation d'attaque sur la durée d'anticipation,
        // pour que le coup soit lisible avant l'impact.
        EnsureFloatParameter(controller, ATTACK_SPEED_PARAM, 1f);

        AnimatorStateMachine movement = GetLayer(controller, "Movement Layer", 0);
        AnimatorStateMachine attack   = GetLayer(controller, "Attack Layer", 1);

        int added = 0;

        added += AddState(attack, "GetHit",  COMBAT_PATH + "GetHit.fbx", 1f, null);
        added += AddState(attack, "Stunned", COMBAT_PATH + "StunnedLoop.fbx", 1f, null);
        added += EnsureEnemyPolearmState(attack);
        added += AddState(attack, "OverhandThrow",   EXTERNAL_COMBAT_PATH + "OverhandThrow.anim", 1f, ATTACK_SPEED_PARAM);
        added += AddState(attack, "Buff",            COMBAT_PATH + "Buff.fbx", 1f, ATTACK_SPEED_PARAM);

        // Doublon sur la couche de déplacement : un stagger doit pouvoir couper
        // la marche, pas seulement l'attaque.
        added += AddState(movement, "Stagger", COMBAT_PATH + "GetHit.fbx", 1f, null);

        return added;
    }

    private static int EnsureEnemyPolearmState(AnimatorStateMachine machine) {
        AnimationClip clip = FindOfficialPolearmClip();

        if(clip == null)
            clip = LoadClip(EXTERNAL_COMBAT_PATH + "PolearmThrust_1.anim");

        return EnsureStateMotion(machine, "PolearmThrust_1", clip, 1f, ATTACK_SPEED_PARAM);
    }

    private static AnimationClip FindOfficialPolearmClip() {
        // FBX clips are sub-assets, so searching by filename is more reliable than
        // filtering the main asset as an AnimationClip.
        string[] guids = AssetDatabase.FindAssets("AttackPolearm01", new[] { "Assets" });

        foreach(string guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach(Object asset in assets) {
                AnimationClip clip = asset as AnimationClip;

                if(clip != null && clip.name == OFFICIAL_POLEARM_CLIP)
                    return clip;
            }
        }

        return null;
    }

    private static int EnsureStateMotion(
        AnimatorStateMachine machine,
        string stateName,
        AnimationClip clip,
        float speed,
        string speedParameter,
        bool mirror = false) {

        if(machine == null || clip == null) return 0;

        foreach(ChildAnimatorState child in machine.states) {
            if(child.state == null || child.state.name != stateName) continue;

            bool changed = child.state.motion != clip || child.state.mirror != mirror;
            if(!changed) return 0;

            child.state.motion = clip;
            child.state.mirror = mirror;
            return 1;
        }

        return AddState(machine, stateName, clip, speed, speedParameter);
    }


    private static AnimatorController Load(string path) {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

        if(controller == null)
            Debug.LogError("[CombatSetup] Controller introuvable : " + path);

        return controller;
    }


    private static AnimatorStateMachine GetLayer(AnimatorController controller, string layerName, int fallbackIndex) {

        for(int i = 0; i < controller.layers.Length; i++)
            if(controller.layers[i].name == layerName)
                return controller.layers[i].stateMachine;

        if(fallbackIndex < controller.layers.Length) {
            Debug.LogWarning("[CombatSetup] Couche \"" + layerName + "\" introuvable dans " + controller.name + ", repli sur l'index " + fallbackIndex + ".");
            return controller.layers[fallbackIndex].stateMachine;
        }

        Debug.LogError("[CombatSetup] Aucune couche utilisable pour \"" + layerName + "\" dans " + controller.name + ".");
        return null;
    }


    // Retourne 1 si l'état a été créé, 0 s'il existait déjà ou en cas d'échec.
    private static int AddState(
        AnimatorStateMachine machine,
        string stateName,
        string clipPath,
        float speed,
        string speedParameter,
        bool mirror = false) {

        if(machine == null) return 0;

        foreach(ChildAnimatorState child in machine.states) {
            if(child.state != null && child.state.name == stateName) {
                return 0;
            }
        }

        AnimationClip clip = LoadClip(clipPath);
        if(clip == null) return 0;

        return AddState(machine, stateName, clip, speed, speedParameter, mirror);
    }


    private static int AddState(
        AnimatorStateMachine machine,
        string stateName,
        AnimationClip clip,
        float speed,
        string speedParameter,
        bool mirror = false) {

        if(machine == null || clip == null) return 0;

        foreach(ChildAnimatorState child in machine.states) {
            if(child.state != null && child.state.name == stateName) {
                return 0;
            }
        }

        AnimatorState state = machine.AddState(stateName);
        state.motion = clip;
        state.speed = speed;
        state.mirror = mirror;

        // Pas de transition sortante : le code appelle CrossFade explicitement.
        state.writeDefaultValues = true;

        if(!string.IsNullOrEmpty(speedParameter)) {
            state.speedParameter = speedParameter;
            state.speedParameterActive = true;
        }

        return 1;
    }


    // Les FBX du pack contiennent une pose "tpose" en plus du clip utile.
    private static AnimationClip LoadClip(string path) {

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        if(assets == null || assets.Length == 0) {
            Debug.LogError("[CombatSetup] FBX introuvable : " + path);
            return null;
        }

        List<AnimationClip> clips = new List<AnimationClip>();

        foreach(Object asset in assets) {
            AnimationClip clip = asset as AnimationClip;

            if(clip == null) continue;
            if(clip.name == "tpose") continue;
            if(clip.name.StartsWith("__preview__")) continue;

            clips.Add(clip);
        }

        if(clips.Count == 0) {
            Debug.LogError("[CombatSetup] Aucun clip exploitable dans " + path);
            return null;
        }

        return clips[0];
    }


    private static void EnsureFloatParameter(AnimatorController controller, string parameterName, float defaultValue) {

        foreach(AnimatorControllerParameter parameter in controller.parameters)
            if(parameter.name == parameterName)
                return;

        controller.AddParameter(new AnimatorControllerParameter {
            name = parameterName,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = defaultValue
        });
    }


    [MenuItem("Tools/Monarchy/Combat/Vérifier les clips du pack d'animations")]
    public static void VerifyClips() {

        string[] paths = {
            MOVEMENT_PATH + "RollForward.fbx",
            MOVEMENT_PATH + "RollBackward.fbx",
            MOVEMENT_PATH + "RollLeft.fbx",
            MOVEMENT_PATH + "RollRight.fbx",
            COMBAT_PATH + "MeleeAttack_OneHanded.fbx",
            COMBAT_PATH + "MeleeAttack_TwoHanded.fbx",
            COMBAT_PATH + "PunchLeft.fbx",
            COMBAT_PATH + "PunchRight.fbx",
            COMBAT_PATH + "Buff.fbx",
            COMBAT_PATH + "BlockingLoop.fbx",
            COMBAT_PATH + "StunnedLoop.fbx",
            COMBAT_PATH + "GetHit.fbx",
            EXTERNAL_COMBAT_PATH + "OverhandThrow.anim"
        };

        foreach(string path in paths) {
            AnimationClip clip = LoadClip(path);

            if(clip != null)
                Debug.Log("[CombatSetup] " + path + " -> clip \"" + clip.name + "\" (" + clip.length.ToString("F2") + " s, humanMotion=" + clip.humanMotion + ")");
        }
    }

}

public sealed class CombatSetupAssetPostprocessor : AssetPostprocessor {

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths) {

        foreach(string path in importedAssets) {
            if(path.IndexOf("AttackPolearm01", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            EditorApplication.delayCall += CombatSetup.RefreshPolearmStates;
            return;
        }
    }
}
