using System;
using System.Collections.Generic;
using UnityEngine;
using Grpc.Core;
using Unity.V1;

public class NavigatorLinkee : MonoBehaviour
{
    [SerializeField] private string _hostName;
    [SerializeField] private int _port;
    private Server _server;
    private UnityProtocol _unityProtocol;
    [SerializeField] private List<GameObject> _gameObjectWaypoints;
    [SerializeField] private UnityEngine.AI.NavMeshAgent _navAgent;
    void Awake()
    {

    }
    
    

    void Start()
    {
        Debug.Log($"[NavigatorLinkee] Started monobehavior trying... {_hostName}:{_port}");
        _unityProtocol = new UnityProtocol();
        _unityProtocol.OnNavigateTo += MoveToTarget;
        _unityProtocol.GameObjectNavigators.Add(gameObject);
        foreach (GameObject go in _gameObjectWaypoints){
            _unityProtocol.GameObjectWaypoints.Add(go);
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


    void MoveToTarget(UnityEngine.Vector3 targetPosition)
    {
        // Tells the agent to calculate a path and start moving
        _navAgent.SetDestination(targetPosition);
    }

    async void OnDestroy()
    {
        if (_server != null)
        {
            await _server.ShutdownAsync();
        }
    }
}