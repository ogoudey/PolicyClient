using UnityEngine;
using System.Collections.Generic;
namespace PolicyClient
{
    public abstract class InferenceManager
    {
        protected InferenceManagerSettings inferenceManagerSettings;
        protected InferenceClientSession session;
        
        protected static int actionChunkLength;
        protected static int stepsAtToPrediction;
        protected int currentStep;
        protected Queue<float[]> actionQueue;
        protected bool isPredictionOutGoing;
        private InferenceState _state = InferenceState.Init; // backer
        protected InferenceState state
        {
            get => _state;
            set
            {
                if (value == _state) return;
                Debug.Log($"[Inference] {_state} → {value} (step {currentStep}/{actionChunkLength})");
                _state = value;
            }
        }
        protected Kinematics kinematics;
        
        public InferenceManager(InferenceManagerSettings inferenceManagerSettings, InferenceClientSession session)
        {
            this.inferenceManagerSettings = inferenceManagerSettings;
            actionChunkLength = inferenceManagerSettings.actionChunkLength;
            stepsAtToPrediction = inferenceManagerSettings.stepsAtToPrediction;
            kinematics = inferenceManagerSettings.kinematics;
            // make input sources
            
            this.session = session; // should take a minimal object to send/request from
            actionQueue = new Queue<float[]>();
        }

        /// <summary>
        /// Inference doesn't start by default
        /// </summary>
        public virtual bool Start()
        {
            return false;
        }

        public abstract bool InferFromFrame();

        // Helpers //
        protected Queue<float[]> Queueify(float[][] actionChunk)
        {
            // Validate against what the manager expects
            if (actionChunk.Length != actionChunkLength)
            {
                UnityEngine.Debug.Log($"[Queueify] action chunk of unexpected Length: expected {actionChunkLength} got {actionChunk.Length}");
            }

            Queue<float[]> actionQueue = new Queue<float[]>(actionChunk.Length);
            foreach (float[] action in actionChunk)
                actionQueue.Enqueue(action);
            return actionQueue;
        }
    }

    public enum InferenceState
    {
        Init,
        Waiting,
        Predicting,
        Executing
    }
}