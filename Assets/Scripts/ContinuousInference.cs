using UnityEngine;
using System.Collections.Generic;
namespace PolicyClient
{
    public class ContinuousInference : InferenceManager
    {
        public ContinuousInference(InferenceManagerSettings inferenceManagerSettings, InferenceClientSession session) : base(inferenceManagerSettings, session)
        {
        }
        /// <summary>
        /// Sets inference initial values
        /// </summary>
        public override bool Start()
        {
            currentStep = 0;
            state = InferenceState.Waiting;
            return true;
        }

        public override bool InferFromFrame()
        {
            // DEcide whether to make new prediction, and update state
            if (currentStep >= actionChunkLength)
                state = InferenceState.Waiting;
            else if (currentStep >= stepsAtToPrediction && !isPredictionOutGoing)
            {

                session.MakePrediction(new InferenceRequest());
                state = InferenceState.Predicting;
                isPredictionOutGoing = true;
            } else
                state = InferenceState.Executing;

            // Apply incoming frame if any
            if (state == InferenceState.Waiting || state == InferenceState.Predicting)
            {
                float[][]? actionChunk = session.GetIncomingActionChunk(); // Validate?
                if (actionChunk != null)
                {
                    actionQueue = Queueify(actionChunk);
                }
            }  
            
            if (actionQueue.TryDequeue(out float[] action))
                kinematics.ApplyAction(action);

            currentStep += 1;
            return true;
        }
    }
}