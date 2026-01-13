using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AITest.Learning
{
    /// <summary>
    /// Perceptron-based Target Scorer
    /// 
    /// PROMPT 8: Score targets using weighted feature vector
    /// - Sigmoid activation: ?(w�x + b)
    /// - Online learning: w += ?(target - output)�x
    /// - Stable updates with learning rate decay
    /// </summary>
    [Serializable]
    public class PerceptronScorer
    {
        [Header("Perceptron Parameters")]
        [Tooltip("Feature weights")]
        public float[] weights;
        
        [Tooltip("Bias term")]
        public float bias = 0f;
        
        [Tooltip("Learning rate (for online updates)")]
        [Range(0f, 1f)] public float learningRate = 0.1f;
        
        [Tooltip("Learning rate decay (per update)")]
        [Range(0.9f, 1f)] public float learningRateDecay = 0.999f;
        
        [Tooltip("Use sigmoid activation (vs linear)")]
        public bool useSigmoid = true;
        
        [Header("Statistics")]
        public int updateCount = 0;
        public float currentLearningRate = 0.1f;
        
        // Feature count (must match input dimension)
        public int FeatureCount => weights?.Length ?? 0;

        /// <summary>
        /// Initialize with random weights
        /// </summary>
        public PerceptronScorer(int featureCount, bool randomize = true)
        {
            weights = new float[featureCount];
            
            if (randomize)
            {
                RandomizeWeights();
            }
            else
            {
                // Equal weights (1/N)
                float equalWeight = 1f / featureCount;
                for (int i = 0; i < featureCount; i++)
                {
                    weights[i] = equalWeight;
                }
            }
            
            bias = 0f;
            currentLearningRate = learningRate;
        }

        /// <summary>
        /// Randomize weights (Gaussian distribution)
        /// </summary>
        public void RandomizeWeights()
        {
            for (int i = 0; i < weights.Length; i++)
            {
                // Gaussian random in [-1, 1]
                weights[i] = UnityEngine.Random.Range(-1f, 1f) * 0.5f;
            }
            
            bias = UnityEngine.Random.Range(-0.5f, 0.5f);
        }

        /// <summary>
        /// ? PROMPT 8: Score a feature vector
        /// </summary>
        public float Score(float[] features)
        {
            if (features.Length != weights.Length)
            {
                Debug.LogError($"[PerceptronScorer] Feature count mismatch! Expected {weights.Length}, got {features.Length}");
                return 0f;
            }
            
            // Linear combination: w�x + b
            float activation = bias;
            for (int i = 0; i < features.Length; i++)
            {
                activation += weights[i] * features[i];
            }
            
            // Activation function
            if (useSigmoid)
            {
                return Sigmoid(activation);
            }
            else
            {
                // Clamp to [0, 1] for linear
                return Mathf.Clamp01(activation);
            }
        }

        /// <summary>
        /// Online learning update (optional)
        /// Update rule: w += ?(target - output)�x
        /// </summary>
        public void Update(float[] features, float targetScore)
        {
            float output = Score(features);
            float error = targetScore - output;
            
            // Update weights
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] += currentLearningRate * error * features[i];
            }
            
            // Update bias
            bias += currentLearningRate * error;
            
            // Decay learning rate
            currentLearningRate *= learningRateDecay;
            updateCount++;
        }

        /// <summary>
        /// Sigmoid activation: ?(x) = 1 / (1 + e^(-x))
        /// </summary>
        private float Sigmoid(float x)
        {
            return 1f / (1f + Mathf.Exp(-x));
        }

        /// <summary>
        /// Reset learning state
        /// </summary>
        public void ResetLearning()
        {
            currentLearningRate = learningRate;
            updateCount = 0;
        }

        /// <summary>
        /// Get weight summary (for debugging)
        /// </summary>
        public string GetWeightSummary()
        {
            string summary = $"Weights (bias={bias:F3}):\n";
            for (int i = 0; i < weights.Length; i++)
            {
                summary += $"  w[{i}] = {weights[i]:F3}\n";
            }
            summary += $"Updates: {updateCount}, LR: {currentLearningRate:F4}";
            return summary;
        }
    }

    /// <summary>
    /// Feature vector for target scoring (7 features)
    /// </summary>
    [Serializable]
    public struct TargetFeatures
    {
        public float heatAtTarget;           // 0-1: Heatmap value
        public float distanceToTarget;       // 0-1: Normalized distance (0=far, 1=close)
        public float timeSinceChecked;       // 0-1: For hide spots (0=recent, 1=old)
        public float hideSpotDensity;        // 0-1: Hide spots per room area
        public float proximityToLastHeard;   // 0-1: Closeness to last heard pos
        public float intersectionCentrality; // 0-1: DEPRECATED (was for IntersectionPoints - now always 0)
        public float questLikelihood;        // 0-1: Probability of quest objective

        /// <summary>
        /// Convert to array (for perceptron input)
        /// </summary>
        public float[] ToArray()
        {
            return new float[]
            {
                heatAtTarget,
                distanceToTarget,
                timeSinceChecked,
                hideSpotDensity,
                proximityToLastHeard,
                intersectionCentrality,
                questLikelihood
            };
        }

        /// <summary>
        /// Feature count (must match perceptron dimension)
        /// </summary>
        public static int FeatureCount => 7;

        public override string ToString()
        {
            return $"[Heat:{heatAtTarget:F2} Dist:{distanceToTarget:F2} Check:{timeSinceChecked:F2} " +
                   $"HideDens:{hideSpotDensity:F2} ProxHear:{proximityToLastHeard:F2} " +
                   $"Central:{intersectionCentrality:F2} Quest:{questLikelihood:F2}]";
        }
    }
}
