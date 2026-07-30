using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildRepair {

    private const string BuildFolder = "Builds/Windows";
    private const string ExecutableName = "MonarchyMayhem.exe";

    [MenuItem("Tools/Monarchy/1 - Diagnostic (ne modifie rien)")]
    public static void Diagnose() {
        Process(false);
    }

    [MenuItem("Tools/Monarchy/2 - Reparer les terrains et scripts manquants")]
    public static void Repair() {
        bool confirmed = EditorUtility.DisplayDialog(
            "Reparation du projet",
            "Les prototypes d'arbres invalides seront supprimes des terrains, ainsi que les composants dont le script est manquant, dans toutes les scenes du Build Settings.\n\nLes scenes et les assets seront sauvegardes.",
            "Reparer", "Annuler");

        if(!confirmed) return;

        Process(true);
    }

    [MenuItem("Tools/Monarchy/3 - Build Windows (dossier Builds)")]
    public static void BuildWindows() {

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        if(scenes.Length == 0) {
            Debug.LogError("[BuildRepair] Aucune scene activee dans le Build Settings.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDir = Path.Combine(projectRoot, BuildFolder);
        Directory.CreateDirectory(outputDir);

        BuildPlayerOptions options = new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = Path.Combine(outputDir, ExecutableName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        var summary = BuildPipeline.BuildPlayer(options).summary;

        if(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) {
            Debug.Log("[BuildRepair] Build reussi : " + options.locationPathName);
            if(!Application.isBatchMode) EditorUtility.RevealInFinder(options.locationPathName);
        } else {
            Debug.LogError("[BuildRepair] Build " + summary.result + " (" + summary.totalErrors + " erreurs). Details dans la Console.");
        }
    }

    [MenuItem("Tools/Monarchy/4 - Chercher les references cassees")]
    public static void ScanBrokenReferences() {

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== REFERENCES CASSEES ===");

        foreach(string scenePath in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)) {

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine("\n--- " + scenePath);

            ScanTerrainData(report);

            foreach(GameObject root in scene.GetRootGameObjects()) {

                foreach(Component component in root.GetComponentsInChildren<Component>(true)) {

                    if(component == null) continue;

                    SerializedObject serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();

                    while(property.NextVisible(true)) {

                        if(property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if(property.objectReferenceValue != null) continue;

                        // Reference pointant sur un objet disparu : le champ est vide mais l'id est encore la
                        EntityId id = property.objectReferenceEntityIdValue;

                        if(id.Equals(default(EntityId))) continue;

                        report.AppendLine("  " + GetPath(component.transform) + " [" + component.GetType().Name + "] "
                            + property.propertyPath + " -> reference perdue (id " + id + ")");
                    }
                }
            }
        }

        Debug.Log("[BuildRepair]\n" + report);
    }

    // Scan complet : objets de scene ET tous les assets dont la scene depend
    // (materiaux, TerrainData, controllers...), proprietes cachees comprises.
    [MenuItem("Tools/Monarchy/6 - Scan profond des references")]
    public static void ScanDeep() {

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== SCAN PROFOND ===");

        HashSet<string> seenAssets = new HashSet<string>();

        foreach(string scenePath in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)) {

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine("\n--- " + scenePath);

            foreach(GameObject root in scene.GetRootGameObjects()) {

                foreach(Component component in root.GetComponentsInChildren<Component>(true)) {

                    if(component == null) continue;

                    ReportBroken(component, GetPath(component.transform) + " [" + component.GetType().Name + "]", report);
                }
            }

            foreach(string dependency in AssetDatabase.GetDependencies(scenePath, true)) {

                if(!seenAssets.Add(dependency)) continue;
                if(dependency.EndsWith(".cs") || dependency.EndsWith(".shader")) continue;

                foreach(Object asset in AssetDatabase.LoadAllAssetsAtPath(dependency)) {

                    if(asset == null || IsHeavyType(asset)) continue;

                    ReportBroken(asset, dependency + " :: " + asset.name + " [" + asset.GetType().Name + "]", report);
                }
            }
        }

        Debug.Log("[BuildRepair]\n" + report);
    }

    [MenuItem("Tools/Monarchy/7 - Reparer les assets (controllers, materiaux)")]
    public static void FixAssets() {

        string[] extensions = { ".controller", ".overrideController", ".mat", ".asset", ".mixer", ".playable" };

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== REPARATION DES ASSETS ===");

        string[] paths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && extensions.Any(e => p.EndsWith(e, System.StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        int total = 0;

        try {

            for(int i = 0; i < paths.Length; i++) {

                if(EditorUtility.DisplayCancelableProgressBar("Reparation des assets", paths[i], (float) i / paths.Length)) break;

                int fixedCount = 0;

                foreach(Object asset in AssetDatabase.LoadAllAssetsAtPath(paths[i])) {

                    if(asset == null || IsHeavyType(asset)) continue;

                    SerializedObject serialized = new SerializedObject(asset);
                    SerializedProperty property = serialized.GetIterator();
                    bool enterChildren = true;
                    bool touched = false;

                    while(property.Next(enterChildren)) {

                        enterChildren = true;

                        if(property.isArray && property.propertyType != SerializedPropertyType.String) {
                            string elementType = property.arrayElementType;
                            if(elementType == null || !elementType.StartsWith("PPtr<")) enterChildren = false;
                        }

                        if(!IsBrokenReference(property)) continue;

                        property.objectReferenceValue = null;
                        touched = true;
                        fixedCount++;
                    }

                    if(touched) serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if(fixedCount > 0) {
                    report.AppendLine("  " + paths[i] + " : " + fixedCount + " reference(s) nettoyee(s)");
                    total += fixedCount;
                    EditorUtility.SetDirty(AssetDatabase.LoadMainAssetAtPath(paths[i]));
                }
            }

        } finally {
            EditorUtility.ClearProgressBar();
        }

        int removedTransitions = RemoveDeadTransitions(report);

        AssetDatabase.SaveAssets();

        report.AppendLine("\n=== TOTAL ===");
        report.AppendLine("References nettoyees : " + total);
        report.AppendLine("Transitions sans destination supprimees : " + removedTransitions);

        Debug.Log("[BuildRepair]\n" + report);
    }

    // Une transition dont l'etat de destination a ete detruit ne mene nulle part :
    // on la supprime plutot que de laisser un pointeur vide dans le controller.
    private static int RemoveDeadTransitions(StringBuilder report) {

        int removed = 0;

        foreach(string guid in AssetDatabase.FindAssets("t:AnimatorController")) {

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if(!path.StartsWith("Assets/")) continue;

            UnityEditor.Animations.AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);

            if(controller == null) continue;

            int before = removed;

            foreach(UnityEditor.Animations.AnimatorControllerLayer layer in controller.layers) {
                removed += RemoveDeadTransitions(layer.stateMachine);
            }

            if(removed > before) report.AppendLine("  " + path + " : " + (removed - before) + " transition(s) morte(s) supprimee(s)");
        }

        return removed;
    }

    private static int RemoveDeadTransitions(UnityEditor.Animations.AnimatorStateMachine machine) {

        int removed = 0;

        foreach(UnityEditor.Animations.ChildAnimatorState child in machine.states) {

            foreach(UnityEditor.Animations.AnimatorStateTransition transition in child.state.transitions) {

                if(transition.destinationState != null || transition.destinationStateMachine != null) continue;
                if(transition.isExit) continue;

                child.state.RemoveTransition(transition);
                removed++;
            }
        }

        foreach(UnityEditor.Animations.ChildAnimatorStateMachine child in machine.stateMachines) {
            removed += RemoveDeadTransitions(child.stateMachine);
        }

        return removed;
    }

    private static bool IsHeavyType(Object asset) {
        return asset is Mesh || asset is Texture || asset is AudioClip || asset is Shader
            || asset is Font || asset is TextAsset || asset is AnimationClip;
    }

    private static void ReportBroken(Object target, string label, StringBuilder report) {

        SerializedProperty property = new SerializedObject(target).GetIterator();
        bool enterChildren = true;

        while(property.Next(enterChildren)) {

            enterChildren = true;

            // Ne pas descendre dans les gros tableaux qui ne contiennent pas de references
            if(property.isArray && property.propertyType != SerializedPropertyType.String) {
                string elementType = property.arrayElementType;
                if(elementType == null || !elementType.StartsWith("PPtr<")) enterChildren = false;
            }

            if(!IsBrokenReference(property)) continue;

            report.AppendLine("  " + label + " " + property.propertyPath
                + " -> reference perdue (id " + property.objectReferenceEntityIdValue + ")");
        }
    }

    // Une reference "perdue" garde un id vers un objet disparu : c'est ce PPtr fantome
    // qui fait planter la serialisation du player. On remet le champ a vide.
    [MenuItem("Tools/Monarchy/5 - Reparer les references cassees")]
    public static void FixBrokenReferences() {

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== REPARATION DES REFERENCES ===");

        int prefabsFixed = 0;
        int prefabRefs = 0;

        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        try {

            for(int i = 0; i < guids.Length; i++) {

                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if(EditorUtility.DisplayCancelableProgressBar("Reparation des prefabs", path, (float) i / guids.Length)) break;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if(asset == null || CountBrokenReferences(asset) == 0) continue;

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                int fixedCount = FixBrokenReferences(contents);

                if(fixedCount > 0) {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    prefabsFixed++;
                    prefabRefs += fixedCount;
                    report.AppendLine("  " + path + " : " + fixedCount + " reference(s) nettoyee(s)");
                }

                PrefabUtility.UnloadPrefabContents(contents);
            }

        } finally {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();

        int sceneRefs = 0;

        foreach(string scenePath in EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)) {

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int fixedCount = 0;

            foreach(GameObject root in scene.GetRootGameObjects()) {

                // Les instances de prefab heritent de la source, deja corrigee au-dessus
                if(PrefabUtility.IsPartOfPrefabInstance(root)) continue;

                fixedCount += FixBrokenReferences(root);
            }

            if(fixedCount > 0) {
                report.AppendLine("  " + scenePath + " : " + fixedCount + " reference(s) nettoyee(s)");
                sceneRefs += fixedCount;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        report.AppendLine("\n=== TOTAL ===");
        report.AppendLine("Prefabs corriges : " + prefabsFixed + " (" + prefabRefs + " references)");
        report.AppendLine("References corrigees dans les scenes : " + sceneRefs);

        Debug.Log("[BuildRepair]\n" + report);
    }

    private static int CountBrokenReferences(GameObject root) {

        int count = 0;

        foreach(Component component in root.GetComponentsInChildren<Component>(true)) {

            if(component == null) continue;

            SerializedProperty property = new SerializedObject(component).GetIterator();

            while(property.NextVisible(true)) {
                if(IsBrokenReference(property)) count++;
            }
        }

        return count;
    }

    private static int FixBrokenReferences(GameObject root) {

        int count = 0;

        foreach(Component component in root.GetComponentsInChildren<Component>(true)) {

            if(component == null) continue;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            bool touched = false;

            while(property.NextVisible(true)) {

                if(!IsBrokenReference(property)) continue;

                property.objectReferenceValue = null;
                touched = true;
                count++;
            }

            if(touched) serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        return count;
    }

    private static bool IsBrokenReference(SerializedProperty property) {

        if(property.propertyType != SerializedPropertyType.ObjectReference) return false;
        if(property.objectReferenceValue != null) return false;

        return !property.objectReferenceEntityIdValue.Equals(default(EntityId));
    }

    // Les TerrainData sont des assets a part entiere : un TerrainCollider peut en utiliser un
    // sans qu'aucun composant Terrain n'existe dans la scene.
    [MenuItem("Tools/Monarchy/8 - Verifier les TerrainData")]
    public static void ScanTerrainAssets() {

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== TERRAINDATA ===");

        foreach(string guid in AssetDatabase.FindAssets("t:TerrainData")) {

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if(!path.StartsWith("Assets/")) continue;

            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);

            if(data == null) continue;

            report.AppendLine("\n--- " + path);

            TreePrototype[] trees = data.treePrototypes;

            for(int i = 0; i < trees.Length; i++) {
                string reason;
                if(!IsValidTreePrefab(trees[i].prefab, out reason)) {
                    report.AppendLine("  arbre [" + i + "] "
                        + (trees[i].prefab != null ? trees[i].prefab.name : "(vide)") + " : " + reason);
                }
            }

            DetailPrototype[] details = data.detailPrototypes;

            for(int i = 0; i < details.Length; i++) {

                if(details[i].usePrototypeMesh) {
                    if(details[i].prototype == null) report.AppendLine("  detail [" + i + "] : prefab manquant");
                } else {
                    if(details[i].prototypeTexture == null) report.AppendLine("  detail [" + i + "] : texture manquante");
                }
            }

            TerrainLayer[] layers = data.terrainLayers;

            for(int i = 0; i < layers.Length; i++) {
                if(layers[i] == null) report.AppendLine("  couche [" + i + "] : manquante");
                else if(layers[i].diffuseTexture == null) report.AppendLine("  couche [" + i + "] '" + layers[i].name + "' : texture manquante");
            }

            report.AppendLine("  (" + trees.Length + " arbres, " + details.Length + " details, "
                + layers.Length + " couches, " + data.treeInstances.Length + " instances)");
        }

        Debug.Log("[BuildRepair]\n" + report);
    }

    [MenuItem("Tools/Monarchy/10 - Verifier les Animator")]
    public static void ScanControllers() {

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== ANIMATOR CONTROLLERS ===");

        foreach(string guid in AssetDatabase.FindAssets("t:AnimatorController")) {

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if(!path.StartsWith("Assets/")) continue;

            UnityEditor.Animations.AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);

            if(controller == null) continue;

            List<string> problems = new List<string>();

            foreach(UnityEditor.Animations.AnimatorControllerLayer layer in controller.layers) {
                CheckStates(layer.stateMachine, layer.name, problems);
            }

            if(problems.Count > 0) {
                report.AppendLine("\n--- " + path);
                foreach(string problem in problems) report.AppendLine("  " + problem);
            }
        }

        Debug.Log("[BuildRepair]\n" + report);
    }

    private static void CheckStates(UnityEditor.Animations.AnimatorStateMachine machine, string layer, List<string> problems) {

        foreach(UnityEditor.Animations.ChildAnimatorState child in machine.states) {

            if(child.state.motion == null) problems.Add("[" + layer + "] etat '" + child.state.name + "' sans animation");

            foreach(UnityEditor.Animations.AnimatorStateTransition transition in child.state.transitions) {
                if(transition.destinationState == null && transition.destinationStateMachine == null && !transition.isExit) {
                    problems.Add("[" + layer + "] transition depuis '" + child.state.name + "' sans destination");
                }
            }
        }

        foreach(UnityEditor.Animations.ChildAnimatorStateMachine child in machine.stateMachines) {
            CheckStates(child.stateMachine, layer, problems);
        }
    }

    [MenuItem("Tools/Monarchy/9 - Reparer les TerrainData")]
    public static void FixTerrainAssets() {

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== REPARATION DES TERRAINDATA ===");

        foreach(string guid in AssetDatabase.FindAssets("t:TerrainData")) {

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if(!path.StartsWith("Assets/")) continue;

            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);

            if(data == null) continue;

            bool changed = false;

            TerrainLayer[] layers = data.terrainLayers;
            TerrainLayer[] validLayers = layers.Where(l => l != null).ToArray();

            if(validLayers.Length != layers.Length) {
                data.terrainLayers = validLayers;
                report.AppendLine("  " + path + " : " + (layers.Length - validLayers.Length) + " couche(s) nulle(s) retiree(s)");
                changed = true;
            }

            DetailPrototype[] details = data.detailPrototypes;
            DetailPrototype[] validDetails = details.Where(IsValidDetail).ToArray();

            if(validDetails.Length != details.Length) {

                try {
                    data.detailPrototypes = validDetails;
                    report.AppendLine("  " + path + " : " + (details.Length - validDetails.Length) + " detail(s) invalide(s) retire(s)");
                    changed = true;
                } catch(System.Exception e) {
                    report.AppendLine("  " + path + " : echec sur les details (" + e.Message + ")");
                }
            }

            if(changed) EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();

        Debug.Log("[BuildRepair]\n" + report);
    }

    private static bool IsValidDetail(DetailPrototype detail) {
        return detail.usePrototypeMesh ? detail.prototype != null : detail.prototypeTexture != null;
    }

    private static void ScanTerrainData(StringBuilder report) {

        foreach(Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {

            TerrainData data = terrain.terrainData;

            if(data == null) {
                report.AppendLine("  terrain '" + terrain.name + "' : TerrainData nul");
                continue;
            }

            TerrainLayer[] layers = data.terrainLayers;

            for(int i = 0; i < layers.Length; i++) {
                if(layers[i] == null) report.AppendLine("  terrain '" + terrain.name + "' : TerrainLayer [" + i + "] nul");
                else if(layers[i].diffuseTexture == null) report.AppendLine("  terrain '" + terrain.name + "' : TerrainLayer [" + i + "] '" + layers[i].name + "' sans texture");
            }

            DetailPrototype[] details = data.detailPrototypes;

            for(int i = 0; i < details.Length; i++) {

                if(details[i].usePrototypeMesh) {
                    if(details[i].prototype == null) report.AppendLine("  terrain '" + terrain.name + "' : DetailPrototype [" + i + "] sans prefab");
                } else {
                    if(details[i].prototypeTexture == null) report.AppendLine("  terrain '" + terrain.name + "' : DetailPrototype [" + i + "] sans texture");
                }
            }

            TreePrototype[] trees = data.treePrototypes;

            for(int i = 0; i < trees.Length; i++) {
                if(trees[i].prefab == null) report.AppendLine("  terrain '" + terrain.name + "' : TreePrototype [" + i + "] sans prefab");
            }
        }
    }

    // Dichotomie : build une copie de la scene privee d'une partie de sa hierarchie,
    // pour localiser l'objet qui fait planter la serialisation du player.
    // Usage : -executeMethod BuildRepair.BisectBuild -scene <path> -from N -to M
    public static void BisectBuild() {

        string scenePath = "Assets/Scenes/Village.unity";
        int from = 0;
        int to = int.MaxValue;

        string[] args = System.Environment.GetCommandLineArgs();

        for(int i = 0; i < args.Length - 1; i++) {
            if(args[i] == "-scene") scenePath = args[i + 1];
            if(args[i] == "-from") from = int.Parse(args[i + 1]);
            if(args[i] == "-to") to = int.Parse(args[i + 1]);
        }

        string parentPath = null;

        for(int i = 0; i < args.Length - 1; i++) {
            if(args[i] == "-parent") parentPath = args[i + 1];
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();

        StringBuilder report = new StringBuilder();
        Transform[] candidates;

        if(string.IsNullOrEmpty(parentPath)) {

            candidates = roots.Select(r => r.transform).ToArray();

        } else {

            Transform parent = FindByPath(roots, parentPath);

            if(parent == null) {
                Debug.LogError("[Bisect] introuvable : " + parentPath);
                return;
            }

            // Les autres racines sont deja innocentees : on les enleve pour aller plus vite
            foreach(GameObject root in roots) {
                if(root != parent.root.gameObject) Object.DestroyImmediate(root);
            }

            candidates = new Transform[parent.childCount];
            for(int i = 0; i < parent.childCount; i++) candidates[i] = parent.GetChild(i);
        }

        report.AppendLine("[Bisect] " + scenePath + " parent=" + (parentPath ?? "<racines>")
            + " : garde [" + from + " - " + to + "] sur " + candidates.Length);

        for(int i = 0; i < candidates.Length; i++) {
            report.AppendLine("  [" + i + "] " + candidates[i].name + (i >= from && i <= to ? "   GARDE" : "   supprime"));
        }

        Debug.Log(report.ToString());

        for(int i = 0; i < candidates.Length; i++) {
            if(i < from || i > to) Object.DestroyImmediate(candidates[i].gameObject);
        }

        string strip = null;

        for(int i = 0; i < args.Length - 1; i++) {
            if(args[i] == "-strip") strip = args[i + 1];
        }

        if(!string.IsNullOrEmpty(strip)) {

            string[] types = strip.Split(',');
            int stripped = 0;

            foreach(GameObject root in scene.GetRootGameObjects()) {

                foreach(Component component in root.GetComponentsInChildren<Component>(true)) {

                    if(component == null || !types.Contains(component.GetType().Name)) continue;

                    try {
                        Object.DestroyImmediate(component);
                        stripped++;
                    } catch(System.Exception e) {
                        Debug.LogWarning("[Bisect] " + component.GetType().Name + " non supprimable : " + e.Message);
                    }
                }
            }

            Debug.Log("[Bisect] composants retires (" + strip + ") : " + stripped);
        }

        if(args.Contains("-listOnly")) {

            StringBuilder tree = new StringBuilder();
            tree.AppendLine("[Bisect] contenu conserve :");

            foreach(GameObject root in scene.GetRootGameObjects()) DumpTree(root.transform, 0, tree);

            Debug.Log(tree.ToString());
            return;
        }

        string tempScene = "Assets/Scenes/__bisect.unity";
        EditorSceneManager.SaveScene(scene, tempScene, true);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDir = Path.Combine(projectRoot, "Builds/Bisect");
        Directory.CreateDirectory(outputDir);

        BuildPlayerOptions options = new BuildPlayerOptions {
            scenes = new string[] { tempScene },
            locationPathName = Path.Combine(outputDir, "bisect.exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        var summary = BuildPipeline.BuildPlayer(options).summary;

        Debug.Log("[Bisect] RESULTAT : " + summary.result + " (" + summary.totalErrors + " erreurs)");

        AssetDatabase.DeleteAsset(tempScene);
    }

    private static void DumpTree(Transform transform, int depth, StringBuilder output) {

        string indent = new string(' ', depth * 2);
        string components = string.Join(", ", transform.gameObject.GetComponents<Component>()
            .Select(c => c == null ? "<SCRIPT MANQUANT>" : c.GetType().Name));

        output.AppendLine(indent + transform.name + "  (" + components + ")");

        for(int i = 0; i < transform.childCount; i++) DumpTree(transform.GetChild(i), depth + 1, output);
    }

    private static Transform FindByPath(GameObject[] roots, string path) {

        string[] parts = path.Split('/');

        foreach(GameObject root in roots) {

            if(root.name != parts[0]) continue;

            Transform current = root.transform;

            for(int i = 1; i < parts.Length && current != null; i++) current = current.Find(parts[i]);

            if(current != null) return current;
        }

        return null;
    }

    private static void Process(bool apply) {

        string[] scenePaths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        StringBuilder report = new StringBuilder();
        report.AppendLine(apply ? "=== REPARATION ===" : "=== DIAGNOSTIC (aucune modification) ===");

        int totalPrototypes = 0;
        int totalInstances = 0;
        int totalComponents = 0;
        int totalLightingData = 0;

        // Avant les scenes : corriger la source met a jour toutes les instances
        report.AppendLine("\n--- Prefabs");
        int totalPrefabComponents = CleanPrefabAssets(apply, report);

        HashSet<TerrainData> processedTerrains = new HashSet<TerrainData>();

        foreach(string scenePath in scenePaths) {

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine("\n--- " + scenePath);

            bool dirty = false;

            int prototypes;
            int instances;
            dirty |= CleanTerrains(scene, apply, processedTerrains, report, out prototypes, out instances);
            totalPrototypes += prototypes;
            totalInstances += instances;

            int components;
            dirty |= CleanMissingScripts(scene, apply, report, out components);
            totalComponents += components;

            if(CleanForeignLightingData(scene, apply, report)) {
                totalLightingData++;
                dirty = true;
            }

            if(apply && dirty) {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.AppendLine("  -> scene sauvegardee");
            }
        }

        if(apply) AssetDatabase.SaveAssets();

        report.AppendLine("\n=== TOTAL ===");
        report.AppendLine("Prototypes d'arbres invalides : " + totalPrototypes);
        report.AppendLine("Instances d'arbres concernees : " + totalInstances);
        report.AppendLine("Composants au script manquant (scenes) : " + totalComponents);
        report.AppendLine("Composants au script manquant (prefabs) : " + totalPrefabComponents);
        report.AppendLine("Lighting data etrangers : " + totalLightingData);

        Debug.Log("[BuildRepair]\n" + report);
    }

    private static bool CleanTerrains(Scene scene, bool apply, HashSet<TerrainData> processed, StringBuilder report, out int removedPrototypes, out int removedInstances) {

        removedPrototypes = 0;
        removedInstances = 0;
        bool changed = false;

        foreach(Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {

            TerrainData data = terrain.terrainData;

            if(data == null || !processed.Add(data)) continue;

            TreePrototype[] prototypes = data.treePrototypes;
            List<int> kept = new List<int>();

            for(int i = 0; i < prototypes.Length; i++) {

                string reason;

                if(IsValidTreePrefab(prototypes[i].prefab, out reason)) {
                    kept.Add(i);
                } else {
                    string label = prototypes[i].prefab != null ? prototypes[i].prefab.name : "(vide)";
                    report.AppendLine("  arbre invalide [" + i + "] " + label + " : " + reason);
                }
            }

            if(kept.Count == prototypes.Length) continue;

            int[] remap = new int[prototypes.Length];
            for(int i = 0; i < remap.Length; i++) remap[i] = -1;
            for(int i = 0; i < kept.Count; i++) remap[kept[i]] = i;

            TreeInstance[] instances = data.treeInstances;
            List<TreeInstance> survivors = new List<TreeInstance>(instances.Length);

            foreach(TreeInstance instance in instances) {

                if(instance.prototypeIndex < 0 || instance.prototypeIndex >= remap.Length) continue;
                if(remap[instance.prototypeIndex] < 0) continue;

                TreeInstance copy = instance;
                copy.prototypeIndex = remap[instance.prototypeIndex];
                survivors.Add(copy);
            }

            removedPrototypes += prototypes.Length - kept.Count;
            removedInstances += instances.Length - survivors.Count;

            report.AppendLine("  terrain '" + terrain.name + "' : " + (prototypes.Length - kept.Count) + " prototype(s) et "
                + (instances.Length - survivors.Count) + " instance(s) sur " + instances.Length + " a supprimer");

            if(!apply) continue;

            // On vide d'abord pour qu'aucune instance ne pointe sur un index hors limites
            data.SetTreeInstances(new TreeInstance[0], false);
            data.treePrototypes = kept.Select(i => prototypes[i]).ToArray();
            data.SetTreeInstances(survivors.ToArray(), false);
            terrain.Flush();

            EditorUtility.SetDirty(data);
            changed = true;
        }

        return changed;
    }

    private static bool IsValidTreePrefab(GameObject prefab, out string reason) {

        if(prefab == null) {
            reason = "prefab manquant";
            return false;
        }

        if(prefab.GetComponent<LODGroup>() != null || prefab.GetComponent<BillboardRenderer>() != null) {
            reason = null;
            return true;
        }

        MeshFilter filter = prefab.GetComponent<MeshFilter>();
        MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();

        if(renderer != null && filter != null && filter.sharedMesh != null) {
            reason = null;
            return true;
        }

        if(prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) {
            reason = "personnage anime (SkinnedMeshRenderer) : inutilisable comme arbre de terrain";
            return false;
        }

        reason = "pas de MeshRenderer + MeshFilter valide sur l'objet racine";
        return false;
    }

    private static bool CleanMissingScripts(Scene scene, bool apply, StringBuilder report, out int removed) {

        removed = 0;
        bool changed = false;

        foreach(GameObject root in scene.GetRootGameObjects()) {

            foreach(Transform transform in root.GetComponentsInChildren<Transform>(true)) {

                GameObject target = transform.gameObject;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);

                if(count == 0) continue;

                if(PrefabUtility.IsPartOfPrefabInstance(target)) {
                    report.AppendLine("  script manquant sur '" + GetPath(transform) + "' : instance de prefab, a corriger sur le prefab source");
                    continue;
                }

                report.AppendLine("  script manquant sur '" + GetPath(transform) + "' (" + count + ")");
                removed += count;

                if(!apply) continue;

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
                changed = true;
            }
        }

        return changed;
    }

    private static int CleanPrefabAssets(bool apply, StringBuilder report) {

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int total = 0;

        try {

            for(int i = 0; i < guids.Length; i++) {

                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if(EditorUtility.DisplayCancelableProgressBar("Analyse des prefabs", path, (float) i / guids.Length)) break;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if(asset == null) continue;

                int count = CountMissingScripts(asset);

                if(count == 0) continue;

                report.AppendLine("  " + path + " : " + count + " script(s) manquant(s)");
                total += count;

                if(!apply) continue;

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                int removed = 0;

                foreach(Transform transform in contents.GetComponentsInChildren<Transform>(true)) {

                    if(PrefabUtility.IsPartOfPrefabInstance(transform.gameObject)) continue;

                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                }

                if(removed > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);

                PrefabUtility.UnloadPrefabContents(contents);

                report.AppendLine("    -> " + removed + " supprime(s)");
            }

        } finally {
            EditorUtility.ClearProgressBar();
        }

        return total;
    }

    private static int CountMissingScripts(GameObject root) {

        int count = 0;

        foreach(Transform transform in root.GetComponentsInChildren<Transform>(true)) {
            count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        }

        return count;
    }

    // Une scene qui pointe sur le LightingData d'une autre scene : lightmaps invalides,
    // et Unity 6 plante en essayant de les serialiser dans le player.
    private static bool CleanForeignLightingData(Scene scene, bool apply, StringBuilder report) {

        LightingDataAsset data = Lightmapping.lightingDataAsset;

        if(data == null) return false;

        string assetPath = AssetDatabase.GetAssetPath(data);
        string expectedFolder = scene.path.Substring(0, scene.path.Length - ".unity".Length) + "/";

        if(assetPath.StartsWith(expectedFolder)) return false;

        report.AppendLine("  lighting data etranger a la scene : " + assetPath);

        if(!apply) return true;

        Lightmapping.lightingDataAsset = null;
        return true;
    }

    private static string GetPath(Transform transform) {

        string path = transform.name;

        while(transform.parent != null) {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}
