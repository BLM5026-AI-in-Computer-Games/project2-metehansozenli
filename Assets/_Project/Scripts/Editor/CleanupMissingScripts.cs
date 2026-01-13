using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Project.Editor
{
    /// <summary>
    /// Kayýp script referanslarýný temizler
    /// Tools > Facility Protocol > Cleanup Missing Scripts
    /// </summary>
    public class CleanupMissingScripts : EditorWindow
    {
        private int removedCount = 0;

        [MenuItem("Tools/Facility Protocol/Cleanup Missing Scripts")]
        public static void ShowWindow()
        {
            GetWindow<CleanupMissingScripts>("Cleanup Scripts");
        }

        private void OnGUI()
        {
            GUILayout.Label("Missing Script Cleanup Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Bu tool tüm GameObject'lerden kayýp script referanslarýný kaldýrýr.\n\n" +
                "?? DÝKKAT: Bu iþlem geri alýnamaz!\n" +
                "Önce scene'i kaydettiðinizden emin olun.",
                MessageType.Warning
            );

            EditorGUILayout.Space();

            if (GUILayout.Button("Clean Current Scene", GUILayout.Height(40)))
            {
                CleanCurrentScene();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Clean All Scenes", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clean All Scenes",
                    "Bu iþlem tüm scene'lerdeki kayýp script'leri temizler!\n\n" +
                    "Devam etmek istediðinizden emin misiniz?",
                    "Evet", "Ýptal"))
                {
                    CleanAllScenes();
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Find Missing Scripts", GUILayout.Height(30)))
            {
                FindMissingScripts();
            }

            if (removedCount > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"? {removedCount} kayýp script referansý temizlendi!", MessageType.Info);
            }
        }

        private void CleanCurrentScene()
        {
            removedCount = 0;
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            foreach (var obj in rootObjects)
            {
                removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                
                // Child object'leri de temizle
                var children = obj.GetComponentsInChildren<Transform>(true);
                foreach (var child in children)
                {
                    if (child.gameObject != obj)
                    {
                        removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    }
                }
            }

            Debug.Log($"<color=lime>[Cleanup] ? {removedCount} missing script removed from scene: {scene.name}</color>");
            EditorUtility.DisplayDialog("Cleanup Complete",
                $"{removedCount} kayýp script referansý temizlendi!\n\n" +
                "Scene: " + scene.name,
                "OK");
        }

        private void CleanAllScenes()
        {
            removedCount = 0;
            string[] scenePaths = System.Array.ConvertAll(
                EditorBuildSettings.scenes,
                scene => scene.path
            );

            var currentScene = SceneManager.GetActiveScene();
            string currentScenePath = currentScene.path;

            foreach (var scenePath in scenePaths)
            {
                if (string.IsNullOrEmpty(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath);
                var rootObjects = scene.GetRootGameObjects();

                int sceneCount = 0;
                foreach (var obj in rootObjects)
                {
                    sceneCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);

                    var children = obj.GetComponentsInChildren<Transform>(true);
                    foreach (var child in children)
                    {
                        if (child.gameObject != obj)
                        {
                            sceneCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                        }
                    }
                }

                if (sceneCount > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"<color=yellow>[Cleanup] {sceneCount} missing scripts removed from: {scene.name}</color>");
                }

                removedCount += sceneCount;
            }

            // Orijinal scene'e geri dön
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath);
            }

            Debug.Log($"<color=lime>[Cleanup] ? TOTAL: {removedCount} missing scripts removed from all scenes</color>");
            EditorUtility.DisplayDialog("Cleanup Complete",
                $"TOPLAM {removedCount} kayýp script referansý temizlendi!",
                "OK");
        }

        private void FindMissingScripts()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== MISSING SCRIPTS REPORT ===\n");

            int totalMissing = 0;

            foreach (var obj in rootObjects)
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj);
                if (count > 0)
                {
                    report.AppendLine($"? {obj.name}: {count} missing script(s)");
                    totalMissing += count;
                }

                var children = obj.GetComponentsInChildren<Transform>(true);
                foreach (var child in children)
                {
                    if (child.gameObject != obj)
                    {
                        count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                        if (count > 0)
                        {
                            report.AppendLine($"? {child.gameObject.name}: {count} missing script(s)");
                            totalMissing += count;
                        }
                    }
                }
            }

            if (totalMissing == 0)
            {
                report.AppendLine("? No missing scripts found!");
            }
            else
            {
                report.Insert(0, $"Total: {totalMissing} missing script(s)\n\n");
            }

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Missing Scripts Report", report.ToString(), "OK");
        }
    }
}
