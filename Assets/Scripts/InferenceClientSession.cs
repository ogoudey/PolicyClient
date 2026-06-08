using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Byter;
using Netly;
using System;

namespace PolicyClient
{
    public class InferenceClientSession
    {
        private InferenceNetworkSettings settings;
        private SessionState state = SessionState.Init;
        public bool connected {get;}
        private TCP.Client client = new TCP.Client(isFraming: true);
        private volatile InferenceResponse? lastResponse;
        public InferenceClientSession(InferenceNetworkSettings inferenceNetworkSettings)
        {
            this.settings = inferenceNetworkSettings;
            client.On.Open(() =>
            {
                Debug.Log($"[InferenceClientSession] Connected to " +
                                $"{settings.ipAddress}:{settings.Port}");
            });

            client.On.Close(() =>
            {
                Debug.Log("[InferenceClientSession] Disconnected.");
            });

            client.On.Error((Exception ex) =>
            {
                Debug.LogError($"[InferenceClientSession] Connection error: {ex.Message}");
            });

            client.On.Data((byte[] bytes) =>
            {
                var response = InferenceResponse.FromBytes(bytes);
                if (response == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[InferenceClientSession] Malformed response received – discarding.");
                    return;
                }
                

                lastResponse = response;
            });
        }


        /// <summary>
        /// Opens the TCP connection. Returns false if the attempt fails
        /// (e.g. the host is unreachable or settings are invalid).
        /// </summary>
        public bool Start()
        {
            try
            {
                // Host / Port come from your InferenceNetworkSettings.
                // Adjust property names to match your actual class.
                var host = new Host(settings.ipAddress, settings.Port);
                client.To.Open(host);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"[InferenceClientSession] Start() failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Serializes <paramref name="request"/> and sends it to the server.
        /// The response is cached and retrievable via GetIncomingActionSequence().
        /// Returns false if the client is not connected.
        /// </summary>
        public bool MakePrediction(InferenceRequest request)
        {
            UnityEngine.Debug.LogError($"[InferenceClientSession] Making prediction...");

            try
            {
                client.To.Data(request.ToBytes());
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[InferenceClientSession] MakePrediction() failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Returns the action sequence from the most recently received
        /// server response, or an empty array if none has arrived yet.
        /// </summary>
        public float[][]? GetIncomingActionChunk()
            => lastResponse?.ActionChunk;
    }

    public enum SessionState
    {
        Init,
        Connecting,
        Connected
    }
}