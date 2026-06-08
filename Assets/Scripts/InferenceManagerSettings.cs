using UnityEngine;
using System;

namespace PolicyClient
{
    [Serializable]
    public class InferenceManagerSettings
    {
        [SerializeField] public int actionChunkLength = 50;
        [SerializeField] public int stepsAtToPrediction = 50;

        [SerializeField] public Kinematics kinematics; // Should be monobehavior I think - or not.
    
        public void OnValidate()
        {
            if (stepsAtToPrediction > actionChunkLength)
            {
                UnityEngine.Debug.Log($"Steps at which to make a prediction ({stepsAtToPrediction}) should not be > action chunk length {actionChunkLength}");
            }
        }
    }
}