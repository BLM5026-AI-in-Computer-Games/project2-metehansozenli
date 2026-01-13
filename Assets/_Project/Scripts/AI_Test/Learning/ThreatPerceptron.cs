using UnityEngine;

namespace AITest.Learning
{
    /// <summary>
    /// Neural network threat score calculator
    /// Input: Environmental sensors ? Output: Threat score (0-1)
    /// 
    /// Input neurons (7):
    /// - Distance to player (normalized)
    /// - Line of sight (binary)
    /// - Light level (0-1)
    /// - Heatmap density (0-1)
    /// - Time since contact (normalized)
    /// - Recent sound (binary)
    /// - Visibility score (0-1)
    /// 
    /// Hidden layer: 4 neurons (sigmoid)
    /// Output: Threat score (0-1)
    /// </summary>
    public class ThreatPerceptron : MonoBehaviour
    {
        [Header("Network Architecture")]
        private const int INPUT_SIZE = 7;
        private const int HIDDEN_SIZE = 4;
        private const int OUTPUT_SIZE = 1;
        
        [Header("Training")]
        [Range(0.01f, 0.5f)] 
        [Tooltip("Öğrenme hızı (gradient descent step size)")]
        public float learningRate = 0.1f;
        
        [Tooltip("Online training aktif mi?")]
        public bool trainingEnabled = true;
        
        [Header("Thresholds")]
        [Tooltip("Düşük tehdit eşiği")]
        [Range(0f, 0.5f)] public float lowThreatThreshold = 0.3f;
        
        [Tooltip("Orta tehdit eşiği")]
        [Range(0.3f, 0.7f)] public float mediumThreatThreshold = 0.6f;
        
        [Tooltip("Yüksek tehdit eşiği")]
        [Range(0.6f, 0.9f)] public float highThreatThreshold = 0.8f;
        
        [Header("Debug")]
        public bool debugMode = false;
        public bool debugTraining = false; // ⚡ Separate flag for training logs
        
        // Network weights: Input ? Hidden ? Output
        private float[,] weightsInputHidden;   // [7 x 4]
        private float[,] weightsHiddenOutput;  // [4 x 1]
        private float[] biasHidden;            // [4]
        private float biasOutput;              // [1]
        
        // Public outputs
        public float LastThreatScore { get; private set; }
        public int TrainingCount { get; private set; }
        
        private void Awake()
        {
            InitializeWeights();
        }
        
        /// <summary>
        /// Xavier/Glorot initialization for better convergence
        /// </summary>
        private void InitializeWeights()
        {
            weightsInputHidden = new float[INPUT_SIZE, HIDDEN_SIZE];
            weightsHiddenOutput = new float[HIDDEN_SIZE, OUTPUT_SIZE];
            biasHidden = new float[HIDDEN_SIZE];
            
            // Xavier scale for input layer
            float scaleInput = Mathf.Sqrt(2f / INPUT_SIZE);
            
            // Xavier scale for hidden layer
            float scaleHidden = Mathf.Sqrt(2f / HIDDEN_SIZE);
            
            // Initialize Input ? Hidden weights
            for (int i = 0; i < INPUT_SIZE; i++)
            {
                for (int h = 0; h < HIDDEN_SIZE; h++)
                {
                    weightsInputHidden[i, h] = Random.Range(-scaleInput, scaleInput);
                }
            }
            
            // Initialize Hidden ? Output weights
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                weightsHiddenOutput[h, 0] = Random.Range(-scaleHidden, scaleHidden);
            }
            
            // Initialize biases
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                biasHidden[h] = Random.Range(-0.1f, 0.1f);
            }
            biasOutput = Random.Range(-0.1f, 0.1f);
            
            Debug.Log("<color=lime>[ThreatPerceptron] Neural network initialized with Xavier weights</color>");
        }
        
        /// <summary>
        /// Forward pass: Compute threat score from sensor inputs
        /// </summary>
        /// <param name="distanceToPlayer">Distance to player (0-20m)</param>
        /// <param name="lineOfSight">Is player visible?</param>
        /// <param name="lightLevel">Light level at enemy position (0-1)</param>
        /// <param name="heatmapDensity">Player visit frequency at location (0-1)</param>
        /// <param name="timeSinceContact">Time since last player contact (0-30s)</param>
        /// <param name="hasRecentHear">Recent sound detection?</param>
        /// <param name="visibilityScore">How centered in FOV (0-1)</param>
        /// <returns>Threat score (0-1)</returns>
        public float ComputeThreatScore(
            float distanceToPlayer,
            bool lineOfSight,
            float lightLevel,
            float heatmapDensity,
            float timeSinceContact,
            bool hasRecentHear,
            float visibilityScore)
        {
            // Normalize inputs to [0, 1] range
            float[] inputs = new float[INPUT_SIZE];
            inputs[0] = 1f - Mathf.Clamp01(distanceToPlayer / 20f);  // Close = high
            inputs[1] = lineOfSight ? 1f : 0f;
            inputs[2] = Mathf.Clamp01(lightLevel);
            inputs[3] = Mathf.Clamp01(heatmapDensity);
            inputs[4] = 1f - Mathf.Clamp01(timeSinceContact / 30f);  // Recent = high
            inputs[5] = hasRecentHear ? 1f : 0f;
            inputs[6] = Mathf.Clamp01(visibilityScore);
            
            // Forward pass: Input ? Hidden
            float[] hidden = new float[HIDDEN_SIZE];
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                float sum = biasHidden[h];
                for (int i = 0; i < INPUT_SIZE; i++)
                {
                    sum += inputs[i] * weightsInputHidden[i, h];
                }
                hidden[h] = Sigmoid(sum);
            }
            
            // Forward pass: Hidden ? Output
            float output = biasOutput;
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                output += hidden[h] * weightsHiddenOutput[h, 0];
            }
            
            LastThreatScore = Sigmoid(output);
            
            return LastThreatScore;
        }
        
        /// <summary>
        /// Backpropagation training (supervised learning)
        /// </summary>
        /// <param name="targetThreatScore">Expected threat score (0-1)</param>
        public void Train(
            float distanceToPlayer,
            bool lineOfSight,
            float lightLevel,
            float heatmapDensity,
            float timeSinceContact,
            bool hasRecentHear,
            float visibilityScore,
            float targetThreatScore)
        {
            if (!trainingEnabled) return;
            
            // Normalize inputs
            float[] inputs = new float[INPUT_SIZE];
            inputs[0] = 1f - Mathf.Clamp01(distanceToPlayer / 20f);
            inputs[1] = lineOfSight ? 1f : 0f;
            inputs[2] = Mathf.Clamp01(lightLevel);
            inputs[3] = Mathf.Clamp01(heatmapDensity);
            inputs[4] = 1f - Mathf.Clamp01(timeSinceContact / 30f);
            inputs[5] = hasRecentHear ? 1f : 0f;
            inputs[6] = Mathf.Clamp01(visibilityScore);
            
            // Forward pass (cache activations for backprop)
            float[] hidden = new float[HIDDEN_SIZE];
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                float sum = biasHidden[h];
                for (int i = 0; i < INPUT_SIZE; i++)
                {
                    sum += inputs[i] * weightsInputHidden[i, h];
                }
                hidden[h] = Sigmoid(sum);
            }
            
            float output = biasOutput;
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                output += hidden[h] * weightsHiddenOutput[h, 0];
            }
            float predictedScore = Sigmoid(output);
            
            // Backpropagation: Output layer
            float outputError = targetThreatScore - predictedScore;
            float outputDelta = outputError * SigmoidDerivative(predictedScore);
            
            // Update Hidden ? Output weights
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                weightsHiddenOutput[h, 0] += learningRate * outputDelta * hidden[h];
            }
            biasOutput += learningRate * outputDelta;
            
            // Backpropagation: Hidden layer
            float[] hiddenDeltas = new float[HIDDEN_SIZE];
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                float error = outputDelta * weightsHiddenOutput[h, 0];
                hiddenDeltas[h] = error * SigmoidDerivative(hidden[h]);
            }
            
            // Update Input ? Hidden weights
            for (int i = 0; i < INPUT_SIZE; i++)
            {
                for (int h = 0; h < HIDDEN_SIZE; h++)
                {
                    weightsInputHidden[i, h] += learningRate * hiddenDeltas[h] * inputs[i];
                }
            }
            
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                biasHidden[h] += learningRate * hiddenDeltas[h];
            }
            
            TrainingCount++;
            
            if (debugTraining && TrainingCount % 50 == 0) // ⚡ Only log every 50 training steps
            {
                Debug.Log($"<color=yellow>[ThreatPerceptron] Training #{TrainingCount}: Error={Mathf.Abs(outputError):F4}, Score={predictedScore:F3}→{targetThreatScore:F3}</color>");
            }
        }
        
        /// <summary>
        /// Sigmoid activation function
        /// </summary>
        private float Sigmoid(float x)
        {
            return 1f / (1f + Mathf.Exp(-Mathf.Clamp(x, -10f, 10f))); // Clamp for numerical stability
        }
        
        /// <summary>
        /// Derivative of sigmoid (for backprop)
        /// </summary>
        private float SigmoidDerivative(float sigmoidOutput)
        {
            return sigmoidOutput * (1f - sigmoidOutput);
        }
        
        /// <summary>
        /// Get threat category as string
        /// </summary>
        public string GetThreatCategory()
        {
            if (LastThreatScore < lowThreatThreshold) return "Low";
            if (LastThreatScore < mediumThreatThreshold) return "Medium";
            if (LastThreatScore < highThreatThreshold) return "High";
            return "Critical";
        }
        
        /// <summary>
        /// Get reward multiplier based on threat level
        /// </summary>
        public float GetRewardMultiplier()
        {
            if (LastThreatScore >= highThreatThreshold)
                return 1.5f;  // Critical: Aggressive rewards
            else if (LastThreatScore >= mediumThreatThreshold)
                return 1.2f;  // High threat
            else if (LastThreatScore < lowThreatThreshold)
                return 0.7f;  // Low threat: Conservative
            else
                return 1.0f;  // Medium: Normal
        }
        
        /// <summary>
        /// Save weights to PlayerPrefs
        /// </summary>
        public void SaveWeights()
        {
            for (int i = 0; i < INPUT_SIZE; i++)
            {
                for (int h = 0; h < HIDDEN_SIZE; h++)
                {
                    PlayerPrefs.SetFloat($"threat_w_ih_{i}_{h}", weightsInputHidden[i, h]);
                }
            }
            
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                PlayerPrefs.SetFloat($"threat_w_ho_{h}", weightsHiddenOutput[h, 0]);
                PlayerPrefs.SetFloat($"threat_b_h_{h}", biasHidden[h]);
            }
            
            PlayerPrefs.SetFloat("threat_b_o", biasOutput);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Load weights from PlayerPrefs
        /// </summary>
        public void LoadWeights()
        {
            for (int i = 0; i < INPUT_SIZE; i++)
            {
                for (int h = 0; h < HIDDEN_SIZE; h++)
                {
                    weightsInputHidden[i, h] = PlayerPrefs.GetFloat($"threat_w_ih_{i}_{h}", weightsInputHidden[i, h]);
                }
            }
            
            for (int h = 0; h < HIDDEN_SIZE; h++)
            {
                weightsHiddenOutput[h, 0] = PlayerPrefs.GetFloat($"threat_w_ho_{h}", weightsHiddenOutput[h, 0]);
                biasHidden[h] = PlayerPrefs.GetFloat($"threat_b_h_{h}", biasHidden[h]);
            }
            
            biasOutput = PlayerPrefs.GetFloat("threat_b_o", biasOutput);
        }
    }
}
