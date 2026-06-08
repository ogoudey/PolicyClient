using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace PolicyClient
{
    [Serializable]
    public class InferenceNetworkSettings
    {
        [SerializeField] public string ipAddress;
        [SerializeField] public int Port;
        [SerializeField] public int actionsPerChunk;
        [SerializeField] public PolicyInstructionFormatType policyInstructionFormatType;
        [SerializeField] public PolicyType policyType;
        [SerializeField] public PolicyPath policyPath;
        [SerializeField] private List<VisualShapeEntry> visualShapes = new List<VisualShapeEntry>();
        public Dictionary<string, int[]> VisualShapes {get => visualShapes.ToDictionary(e => e.key, e => e.shape);}
        [SerializeField] public int[] stateShape = new[] { 6 }; 
        [SerializeField] public int[] actionShape = new[] { 6 };

        

    }

    [Serializable]
    public class VisualShapeEntry
    {
        public string key;
        public int[]  shape;
    }

    public enum PolicyInstructionFormatType
    {
        /// <summary>
        /// Output from a LeRobot policy server
        /// </summary>
        LeRobot
    }

    public enum PolicyType
    {
        /// <summary>
        /// Output from a LeRobot policy server
        /// </summary>
        SmolVLA
    }

    public class PolicyPath
    {
        /// <summary>
        /// Output from a LeRobot policy server
        /// </summary>
        public const string LeRobotXyz = "/home/olin/Robotics/Projects/LeRobot/lerobot/outputs/xyz";
        
        // You can easily add more paths here
        public const string LeRobotAbc = "/home/olin/Robotics/Projects/LeRobot/lerobot/outputs/abc";
    }
}