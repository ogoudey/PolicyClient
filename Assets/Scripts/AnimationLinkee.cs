using System;
using System.Collections.Generic;
using UnityEngine;
using Grpc.Core;
using Unity.V1;

public class AnimationLinkee : MonoBehaviour
{
    [Serializable]
    public struct TriggerPair
    {
        public string Key;
        public string Trigger; // Drag and drop animation clips in the Inspector!
    }

    [SerializeField] private List<TriggerPair> _triggerList = new List<TriggerPair>();
    private Dictionary<string, string> _triggerMap = new Dictionary<string, string>();
    [SerializeField] private string _hostName;
    [SerializeField] private int _port;
    private Animator _animator;
    private Server _server;
    private UnityProtocol _unityProtocol;
    void Awake()
    {
        _animator = GetComponent<Animator>();

        foreach (var pair in _triggerList)
        {
            if (!string.IsNullOrEmpty(pair.Key) && pair.Trigger != null && !_triggerMap.ContainsKey(pair.Key))
            {
                _triggerMap.Add(pair.Key, pair.Trigger);
            }
        }
        AnimationDispatcher.Instance.Linkee = this;
    }
    
    

    void Start()
    {
        Debug.Log($"[AnimationLinkee] Started monobehavior trying... {_hostName}:{_port}");
        _unityProtocol = new UnityProtocol();
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

    public bool TryAnimate(string name)
    {
        if (_triggerMap.TryGetValue(name, out string triggerName))
        {
            // Animator.Play expects a state name string, so pass clip.name
            _animator.SetTrigger(triggerName);
            return true;
        }

        return false;
    }

    async void OnDestroy()
    {
        if (_server != null)
        {
            await _server.ShutdownAsync();
        }
    }
}