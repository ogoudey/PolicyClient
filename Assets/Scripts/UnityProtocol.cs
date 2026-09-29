using Unity.V1;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using Google.Protobuf;
using System;
using Grpc.Core;

public class UnityProtocol : AnimateService.AnimateServiceBase
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadActions = new();
    public const string ProtoVersion = "1.0.0";


    public List<GameObject> GameObjectNavigators = new List<GameObject>();
    public List<GameObject> GameObjectWaypoints = new List<GameObject>();
    public List<CustomCameraSettings> Cameras = new List<CustomCameraSettings>();
    public event Action<UnityEngine.Vector3> OnNavigateTo;

    public override Task<VersionResponse> GetVersion(VersionRequest request, ServerCallContext context)
    {
        Debug.Log($"Got GetVersion request");
        return Task.FromResult(new VersionResponse
        {
            ProtoVersion = ProtoVersion,
            ServerVersion = "TEST_VERSION"
        });
    }

    public override Task<ActResponse> Act(ActRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<ActResponse>();
        
        _mainThreadActions.Enqueue(() =>
        {
            try
            {
                bool handled = AnimationDispatcher.Instance.Dispatch(request.ActionName);
                tcs.SetResult(new ActResponse
                {
                    Success = handled,
                    Message = handled ? "ok" : $"Unknown action: {request.ActionName}"
                });
            }
            catch (Exception e)
            {
                tcs.SetResult(new ActResponse { Success = false, Message = e.Message });
            }
        });

        return tcs.Task;
    }

    // -------- NatvigateTo ---------
    public override Task<NavigateToResponse> NavigateTo(NavigateToRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<NavigateToResponse>();

        _mainThreadActions.Enqueue(() =>
        {
            try
            {
                var destination = new UnityEngine.Vector3(
                    request.Destination.X,
                    request.Destination.Y,
                    request.Destination.Z
                );

                OnNavigateTo?.Invoke(destination);

                bool started = true;

                tcs.SetResult(new NavigateToResponse
                {
                    Success = started,
                    Message = started ? "navigating" : $"Unknown entity: {request.EntityId}"
                });
            }
            catch (Exception e)
            {
                tcs.SetResult(new NavigateToResponse { Success = false, Message = e.Message });
            }
        });

        return tcs.Task;
    }

    // ---------- GetEntities ----------

    public override Task<GetEntitiesResponse> GetEntities(GetEntitiesRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<GetEntitiesResponse>();

        _mainThreadActions.Enqueue(() =>
        {
            try
            {
                var response = new GetEntitiesResponse();


                foreach (var obj in GameObjectNavigators)
                {
                    response.Entities.Add(BuildEntity(obj.name, obj.name, "navAgent", obj.transform));
                }

                foreach (var obj in GameObjectWaypoints)
                {
                    response.Entities.Add(BuildEntity(obj.name, obj.name, "waypoint", obj.transform));
                }

                tcs.SetResult(response);
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });

        return tcs.Task;
    }

    // Helper: Transform -> Entity proto. Call this from your object-linking code.
    private static Entity BuildEntity(string id, string name, string type, Transform t)
    {
        return new Entity
        {
            Id = id,
            Name = name,
            Type = type,
            Position = new Unity.V1.Vector3 { X = t.position.x, Y = t.position.y, Z = t.position.z },
            RotationEuler = new Unity.V1.Vector3 { X = t.eulerAngles.x, Y = t.eulerAngles.y, Z = t.eulerAngles.z }
        };
    }



    // GetObservation ----------

    public override Task<GetObservationResponse> GetObservation(GetObservationRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<GetObservationResponse>();

        _mainThreadActions.Enqueue(() =>
        {
            try
            {
                var response = new GetObservationResponse
                {
                    TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };


                foreach (var cam in Cameras)
                {
                    response.Vision[$"{cam.name}"] = TextureToImageProto(cam.camera.RenderToTexture(), useJpeg: false);                    
                }

                // response.State["joint_positions"] = FloatArrayToNDArray(robotArm.GetJointPositions());
                // response.Scalars["sim_time"] = Time.time;

                tcs.SetResult(response);
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });

        return tcs.Task;
    }

    // Helper: Texture2D -> Image proto (PNG-encoded)
    private static Image TextureToImageProto(Texture2D tex, bool useJpeg = false, int jpegQuality = 75)
    {
        byte[] bytes = useJpeg ? tex.EncodeToJPG(jpegQuality) : tex.EncodeToPNG();
        return new Image
        {
            Data = Google.Protobuf.ByteString.CopyFrom(bytes),
            Width = tex.width,
            Height = tex.height,
            Channels = 3, // adjust if using RGBA
            Encoding = useJpeg ? "jpeg" : "png"
        };
    }

    // Helper: raw RGB24 Texture2D -> Image proto, skipping compression entirely
    // (cheaper CPU cost, larger payload — use if latency matters more than bandwidth)
    private static Image TextureToRawImageProto(Texture2D tex)
    {
        byte[] raw = tex.GetRawTextureData(); // assumes tex.format == TextureFormat.RGB24
        return new Image
        {
            Data = Google.Protobuf.ByteString.CopyFrom(raw),
            Width = tex.width,
            Height = tex.height,
            Channels = 3,
            Encoding = "raw_rgb8"
        };
    }

    // Helper: float[] -> NDArray proto
    private static NDArray FloatArrayToNDArray(float[] values, params int[] shape)
    {
        var arr = new NDArray();
        arr.Shape.AddRange(shape.Length > 0 ? shape : new[] { values.Length });
        arr.Data.AddRange(values);
        return arr;
    }

    // Called from Unity's Update() loop — drains queued work
    public void ProcessMainThreadQueue()
    {
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }
}
