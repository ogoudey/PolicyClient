using System;
using System.Collections.Generic;
using UnityEngine;
using Grpc.Core;
using Unity.V1;

public class LayeredLinkee : MonoBehaviour
{
    [SerializeField] private string _hostName;
    [SerializeField] private int _port;
    [SerializeField] private List<CustomCameraSettings> _cameras = new List<CustomCameraSettings>();
    private Server _server;
    private UnityProtocol _unityProtocol;

    void Awake()
    {

    }
    
    

    void Start()
    {
        Debug.Log($"[LayeredLinkee] Started monobehavior trying... {_hostName}:{_port}");
        _unityProtocol = new UnityProtocol();
        foreach (CustomCameraSettings cameraSetting in _cameras){
            _unityProtocol.Cameras.Add(cameraSetting);
        }

        _server = new Server
        {
            Services = { AnimateService.BindService(_unityProtocol) },
            Ports = { new ServerPort(_hostName, _port, ServerCredentials.Insecure) }
        };
        _server.Start();
        Debug.Log($"gRPC server listening on port {_hostName}:{_port}");

    }

    void Update()
    {
        _unityProtocol.ProcessMainThreadQueue();
    }

    async void OnDestroy()
    {
        if (_server != null)
        {
            await _server.ShutdownAsync();
        }
    }
}