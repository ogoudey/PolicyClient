using Newtonsoft.Json;
using System.Collections.Generic;
using Transport;
using Google.Protobuf;
using System;
using UnityEngine;

namespace PolicyClient
{
    public class PolicyFeaturePayload
    {
        public string type;
        public int[] shape;
    }

    public static class LeRobotUtils
    {
        public static PolicySetup SetupPolicy(
            string policyType,
            string pretrainedPath,
            int actionsPerChunk,
            Dictionary<string, int[]> visualShapes,
            int[] stateShape,
            int[] actionShape)
        {
            try
            {
                Dictionary<string, PolicyFeaturePayload> lerobotFeatures = new Dictionary<string, PolicyFeaturePayload>();

                foreach (var kvp in visualShapes)
                    lerobotFeatures[$"observation.images.{kvp.Key}"] = 
                        new PolicyFeaturePayload { type = "VISUAL", shape = kvp.Value };

                lerobotFeatures["observation.state"] = new PolicyFeaturePayload { type = "STATE",  shape = stateShape };
                lerobotFeatures["action"]            = new PolicyFeaturePayload { type = "ACTION", shape = actionShape };

                var payload = new Dictionary<string, object>
                {
                    ["policy_type"]             = policyType,
                    ["pretrained_name_or_path"] = pretrainedPath,
                    ["actions_per_chunk"]       = actionsPerChunk,
                    ["device"]                  = "cuda",
                    ["lerobot_features"]        = lerobotFeatures
                };

                var json  = JsonConvert.SerializeObject(payload);
                var setup = new PolicySetup { Data = ByteString.CopyFromUtf8(json) };
                return setup;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InferenceClientSession] SendPolicyInstructions() failed: {ex.Message}");
                return null;
            }
        }
    }
}