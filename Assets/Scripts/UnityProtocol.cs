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

    // Called from Unity's Update() loop — drains queued work
    public void ProcessMainThreadQueue()
    {
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }
}
