using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using Grpc.Core;
using Google.Protobuf;
using Transport;

using Newtonsoft.Json;

namespace PolicyClient
{
    public class InferenceClientSession
    {
        private InferenceNetworkSettings settings;
        private SessionState state = SessionState.Init;

        public bool connected {get;}
        private volatile float[][] lastActionChunk;
        private GrpcChannel channel;
        private AsyncInference.AsyncInferenceClient client;
        private AsyncClientStreamingCall<Observation, Empty> observationStream;
        private CancellationTokenSource cts;
        public InferenceClientSession(InferenceNetworkSettings inferenceNetworkSettings)
        {
            this.settings = inferenceNetworkSettings;
        }


        /// <summary>
        /// Opens the TCP connection. Returns false if the attempt fails
        /// (e.g. the ipAddress is unreachable or settings are invalid).
        /// </summary>
        public bool Start()
        {
            try
            {
                var handler = new YetAnotherHttpHandler() { Http2Only = true };
                channel = GrpcChannel.ForAddress(
                    $"http://{settings.ipAddress}:{settings.Port}",
                    new GrpcChannelOptions { HttpHandler = handler, DisposeHttpClient = true }
                );

                client = new AsyncInference.AsyncInferenceClient(channel);
                cts = new CancellationTokenSource();

                // Check server is up
                client.Ready(new Empty());

                // Open the observation stream once; we reuse it across MakePrediction() calls
                observationStream = client.SendObservations(cancellationToken: cts.Token);
                Debug.Log($"[InferenceClientSession] Connected to {settings.ipAddress}:{settings.Port}");

                

                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"[InferenceClientSession] Start() failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send policy config to server (call once after Start).
        /// data is whatever PolicySetup.data expects — pass null to send empty.
        /// </summary>
        public bool SendPolicyInstructions()
        {
            PolicySetup? setup = null;
            switch (settings.policyInstructionFormatType)
            {
                case PolicyInstructionFormatType.LeRobot:

                    setup = LeRobotUtils.SetupPolicy(
                        settings.policyType.ToString(),
                        settings.policyPath.ToString(),
                        settings.actionsPerChunk,
                        settings.VisualShapes,
                        settings.stateShape,
                        settings.actionShape
                    );
                    break;
                default:
                    Debug.LogError($"[InferenceClientSession] SendPolicyInstructions() failed: PolicyInstructionFormatType {settings.policyInstructionFormatType} not handled.");
                    break;
            }
            try
            {
                client.SendPolicyInstructions(setup);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InferenceClientSession] SendPolicyInstructions() failed: {ex.Message}");
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
            try
            {
                // Build the Observation protobuf — data is your serialized InferenceRequest
                var observation = new Observation
                {
                    TransferState = TransferState.TransferBegin,
                    Data = ByteString.CopyFrom(request.ToBytes())
                };

                // Write to the open stream (fire and don't await — we're in sync context)
                try
                {
                    observationStream.RequestStream.WriteAsync(observation).GetAwaiter().GetResult();
                }
                catch (Exception ex1)
                {
                    Debug.LogError($"[InferenceClientSession] observationStream.RequestStream.WriteAsync() failed: {ex1.Message}");
                    return false;
                }

                // Fetch actions synchronously
                var actions = client.GetActions(new Empty());
                try
                {
                    lastActionChunk = DeserializeActionChunk(actions.Data.ToByteArray());
                }
                catch (Exception ex12)
                {
                    Debug.LogError($"[InferenceClientSession] DeserializeActionChunk() failed: {ex12.Message}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InferenceClientSession] MakePrediction() failed: {ex.Message}");
                return false;
            }
        }
        
        public void Stop()
        {
            try
            {
                cts?.Cancel();
                observationStream?.RequestStream.CompleteAsync().GetAwaiter().GetResult();
                observationStream?.Dispose();
                channel?.ShutdownAsync().GetAwaiter().GetResult();
                channel?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InferenceClientSession] Stop() error: {ex.Message}");
            }
        }

        // ── Deserialize Actions.data ──────────────────────────────────
        // LeRobot pickles the action chunk into Actions.data.
        // Since we can't unpickle in C#, this assumes you've modified
        // the server to send raw little-endian floats instead (see note below).
        private static float[][] DeserializeActionChunk(byte[] data)
        {
            // Each float is 4 bytes, each action is a fixed-length float[]
            // Layout: [chunkLen : int32][actionLen : int32][float, float, ...]
            int offset = 0;
            int chunkLen  = BitConverter.ToInt32(data, offset); offset += 4;
            int actionLen = BitConverter.ToInt32(data, offset); offset += 4;

            var chunk = new float[chunkLen][];
            for (int i = 0; i < chunkLen; i++)
            {
                chunk[i] = new float[actionLen];
                for (int j = 0; j < actionLen; j++)
                {
                    chunk[i][j] = BitConverter.ToSingle(data, offset);
                    offset += 4;
                }
            }
            return chunk;
        }

        /// <summary>
        /// Returns the action sequence from the most recently received
        /// server response, or null if none has arrived yet.
        /// </summary>
        public float[][]? GetIncomingActionChunk()
        {
            var chunk = lastActionChunk;
            lastActionChunk = null;
            return chunk;
        }
    }

    public enum SessionState
    {
        Init,
        Connecting,
        Connected
    }
}