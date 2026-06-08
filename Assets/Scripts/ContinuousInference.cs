using UnityEngine;
using System.Collections.Generic;
namespace PolicyClient
{
    public class ContinuousInference : InferenceManager
    {
        protected int? stepsAfterPrediction = null;
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
            if (currentStep > actionChunkLength)
            {
                //UnityEngine.Debug.Log($"[Waiting] currentStep >= actionChunkLength ({currentStep} >= {actionChunkLength})");
                state = InferenceState.Waiting;
            }
            else if (currentStep >= stepsAtToPrediction && !isPredictionOutGoing)
            {
                //UnityEngine.Debug.Log($"[Predicting] currentStep >= actionChunkLength");
                session.MakePrediction(new InferenceRequest());
                state = InferenceState.Predicting;
                isPredictionOutGoing = true;
                stepsAfterPrediction = 0;
            }
            else if (state == InferenceState.Waiting && !isPredictionOutGoing)
            {
                //UnityEngine.Debug.Log($"[Predicting] because Waiting");
                session.MakePrediction(new InferenceRequest());
                state = InferenceState.Predicting;
                isPredictionOutGoing = true;
                stepsAfterPrediction = 0;
            }
            else
            {
                //UnityEngine.Debug.Log($"[Executing] {currentStep} in {actionChunkLength}");
                state = InferenceState.Executing;
            }    

            // Apply incoming frame if any
            if (state == InferenceState.Waiting || state == InferenceState.Predicting)
            {
                float[][]? actionChunk = session.GetIncomingActionChunk(); // Validate?
                if (actionChunk != null)
                {
                    actionQueue = Queueify(actionChunk);
                    currentStep = 0;
                    if (stepsAfterPrediction != null)
                    {
                        for (int i = 0; i < stepsAfterPrediction; i++)
                        {
                            actionQueue.Dequeue();
                            currentStep += 1;
                        }
                        stepsAfterPrediction = null;
                    }
                }
            }  
            
            if (actionQueue.TryDequeue(out float[] action))
                kinematics.ApplyAction(action);

            currentStep += 1;
            if (stepsAfterPrediction != null) stepsAfterPrediction += 1;
            return true;
        }
    }
}