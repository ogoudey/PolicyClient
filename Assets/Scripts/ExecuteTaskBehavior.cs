using System;
using UnityEngine;
namespace PolicyClient
{
    public class ExecuteTaskBehavior : BaseBehavior
    {
        [SerializeField] private string languageInput = "Do something useful";
        [SerializeField] private InferenceNetworkSettings taskInferenceNetworkSettings;
        [SerializeField] private InferenceManagerSettings taskInferenceManagerSettings;
        InferenceClientSession taskSession;
        private InferenceManager standardInferencer;
        public bool connected {get => taskSession.connected || taskSession != null;}
        public override bool ready {get => standardInferencer != null;}
        public ExecuteTaskBehavior()
        {
            
        }
        // Essentially Start
        public override bool Initialize()
        {
            try
            {
                taskSession = new InferenceClientSession(taskInferenceNetworkSettings);
                Debug.Log("Session created!");
            }
            catch
            {
                Debug.LogWarning("Couldn't create session!");
                return false;
            }

            if (taskSession.Start())
            {
                Debug.Log("Session started!");
                return true;
            }
            else
            {
                Debug.LogWarning("Couldn't start session!");
                return false;
            }
        }
        // Essentially Update
        public override bool HandleFrame()
        {
            if (standardInferencer == null){
                try
                {
                    standardInferencer = new ContinuousInference(taskInferenceManagerSettings, taskSession);
                    Debug.Log("Manager created!");
                }
                catch
                {
                    Debug.LogWarning("Couldn't create manager!");
                    return false;
                } 
            }
            standardInferencer.InferFromFrame();
            return true;
        }
    }
}