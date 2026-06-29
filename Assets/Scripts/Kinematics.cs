using UnityEngine;

namespace PolicyClient
{
    public enum KinematicsInputType
    {
        /// <summary>
        /// Output from a LeRobot policy server
        /// </summary>
        RawJoints,
        EndEffectorPose
    }
    /// <summary>
    /// Pretty much a placeholder for an arm with joints to apply actions to.
    /// </summary>
    public class Kinematics : MonoBehaviour
    {
        [SerializeField] public KinematicsInputType kinematicsInputType; // Just for Agreement
        /// <summary>
        /// Called by inference manager
        /// </summary>
        public bool ApplyAction(float[] action) // should override in a subclass
        {
            return SetJoints(action);
        }
        /// <summary>
        /// Sets joints - true/false if it works.
        /// </summary>
        public bool SetJoints(float[] jointValues)
        {
            UnityEngine.Debug.Log($"Setting joints to {jointValues} I guess,");
            return true;
        }
    }
}