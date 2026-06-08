using System;
using UnityEngine;

namespace PolicyClient
{
    [Serializable]
    public class InferenceNetworkSettings
    {
        [SerializeField] public string ipAddress;
        [SerializeField] public int Port;
        [SerializeField] private PolicyOutputType policyOutputType;

    }

    public enum PolicyOutputType
    {
        /// <summary>
        /// Output from a LeRobot policy server
        /// </summary>
        LeRobot
    }
}