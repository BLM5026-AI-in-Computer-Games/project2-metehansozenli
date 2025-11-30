using UnityEngine;
using UnityEditor;
using AITest.Learning;
using AITest.Perception;
using AITest.Core;

namespace Project.Editor
{
    /// <summary>
    /// AI_Test scene setup verification tool
    /// Tools > Facility Protocol > Check AI_Test Scene
    /// </summary>
    public class PerceptionMigrationTool : EditorWindow
    {
        private Vector2 scrollPos;

        [MenuItem("Tools/Facility Protocol/Check AI_Test Scene")]
        public static void ShowWindow()
        {
            GetWindow<PerceptionMigrationTool>("AI_Test Scene Check");
        }

        private void OnGUI()
        {
            GUILayout.Label("AI_Test Scene Verification Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Bu tool scene'deki AI_Test component'lerini kontrol eder.\n\n" +
                "? Gerekli: EnemyBrain, QLearner, ThreatPerceptron, Perception\n" +
                "? Gerekli: PlayerController, NoiseEmitter, PlayerStatsMonitor\n" +
                "? Gerekli: Sectorizer, NoiseBus, HeatmapTracker",
                MessageType.Info
            );

            EditorGUILayout.Space();

            if (GUILayout.Button("Check Scene Status", GUILayout.Height(40)))
            {
                CheckSceneStatus();
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Clear PlayerPrefs (Reset Learning)", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear PlayerPrefs", 
                    "Bu iþlem tüm öðrenme verilerini (Q-table, NN weights, hiding stats) siler!\n\n" +
                    "Devam etmek istediðinizden emin misiniz?", 
                    "Evet, Sil", "Ýptal"))
                {
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    Debug.Log("<color=yellow>[AI_Test] PlayerPrefs cleared! All learning data reset.</color>");
                    EditorUtility.DisplayDialog("Baþarýlý", "Tüm öðrenme verileri silindi!", "OK");
                }
            }
        }

        private void CheckSceneStatus()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== AI_TEST SCENE STATUS ===\n");
            
            // Player check
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                report.AppendLine("? PLAYER FOUND");
                report.AppendLine($"   Tag: {player.tag}, Layer: {LayerMask.LayerToName(player.layer)}");
                
                var playerController = player.GetComponent<AITest.Player.PlayerController>();
                var noiseEmitter = player.GetComponent<AITest.Player.NoiseEmitter>();
                var statsMonitor = player.GetComponent<AITest.Player.PlayerStatsMonitor>();
                var rb = player.GetComponent<Rigidbody2D>();
                var col = player.GetComponent<CircleCollider2D>();
                
                report.AppendLine($"   PlayerController: {(playerController ? "?" : "?")}")
                      .AppendLine($"   NoiseEmitter: {(noiseEmitter ? "?" : "?")}")
                      .AppendLine($"   PlayerStatsMonitor: {(statsMonitor ? "?" : "?")}")
                      .AppendLine($"   Rigidbody2D: {(rb ? "?" : "?")} {(rb && rb.gravityScale == 0 ? "(Gravity OK)" : "(?? Set Gravity to 0!)")}")
                      .AppendLine($"   CircleCollider2D: {(col ? "?" : "?")}\n");
            }
            else
            {
                report.AppendLine("? PLAYER NOT FOUND (Tag: Player)\n");
            }
            
            // Enemy check
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            report.AppendLine($"? ENEMIES FOUND: {enemies.Length}\n");
            
            foreach (var enemy in enemies)
            {
                report.AppendLine($"Enemy: {enemy.name}");
                
                var brain = enemy.GetComponent<AITest.Enemy.EnemyBrain>();
                var qlearner = enemy.GetComponent<AITest.Learning.QLearner>();
                var perceptron = enemy.GetComponent<AITest.Learning.ThreatPerceptron>();
                var perception = enemy.GetComponent<AITest.Perception.Perception>();
                var lightSensor = enemy.GetComponent<AITest.Core.LightSensor>();
                var actionPlanner = enemy.GetComponent<AITest.Enemy.ActionPlanner>();
                var pathfinder = enemy.GetComponent<AITest.Pathfinding.Pathfinder>();
                var rb = enemy.GetComponent<Rigidbody2D>();
                
                report.AppendLine($"   EnemyBrain: {(brain ? "?" : "?")}")
                      .AppendLine($"   QLearner: {(qlearner ? "?" : "?")}")
                      .AppendLine($"   ThreatPerceptron: {(perceptron ? "?" : "?")}")
                      .AppendLine($"   Perception: {(perception ? "?" : "?")}")
                      .AppendLine($"   LightSensor: {(lightSensor ? "?" : "?")}")
                      .AppendLine($"   ActionPlanner: {(actionPlanner ? "?" : "?")}")
                      .AppendLine($"   Pathfinder: {(pathfinder ? "?" : "?")}")
                      .AppendLine($"   Rigidbody2D: {(rb ? "?" : "?")} {(rb && rb.gravityScale == 0 ? "(Gravity OK)" : "(?? Set Gravity to 0!)")}\n");
            }
            
            // Managers check
            var sectorizer = FindObjectOfType<AITest.Sector.Sectorizer>();
            var noiseBus = FindObjectOfType<AITest.Core.NoiseBus>();
            var heatmapTracker = FindObjectOfType<AITest.Core.HeatmapTracker>();
            var logger = FindObjectOfType<AITest.Utils.Logger>();
            
            report.AppendLine("MANAGERS:")
                  .AppendLine($"   Sectorizer: {(sectorizer ? "?" : "?")}")
                  .AppendLine($"      Sectors: {sectorizer?.sectors?.Length ?? 0}")
                  .AppendLine($"   NoiseBus: {(noiseBus ? "?" : "?")}")
                  .AppendLine($"   HeatmapTracker: {(heatmapTracker ? "?" : "?")}")
                  .AppendLine($"   Logger: {(logger ? "?" : "?")}\n");
            
            // UI check
            var hud = FindObjectOfType<AITest.UI.LearningHUD>();
            report.AppendLine("UI:")
                  .AppendLine($"   LearningHUD: {(hud ? "?" : "?")}\n");
            
            // PlayerPrefs check
            bool hasQTable = PlayerPrefs.HasKey("QLearner_QTable");
            bool hasNNWeights = PlayerPrefs.HasKey("threat_b_o");
            bool hasHidingStats = PlayerPrefs.HasKey("Sectorizer_HidingStats");
            
            report.AppendLine("SAVED DATA (PlayerPrefs):")
                  .AppendLine($"   Q-Table: {(hasQTable ? "? Saved" : "? Not saved")}")
                  .AppendLine($"   NN Weights: {(hasNNWeights ? "? Saved" : "? Not saved")}")
                  .AppendLine($"   Hiding Stats: {(hasHidingStats ? "? Saved" : "? Not saved")}");
            
            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Scene Status", report.ToString(), "OK");
        }
    }
}
