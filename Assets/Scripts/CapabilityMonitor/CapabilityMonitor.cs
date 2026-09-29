using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public class MonitorDuple
{
    public string CapabilityID;
    public GameObject gameObject;

    [Tooltip("Markdown definition published before any status updates. Optional; a default is generated if empty.")]
    public TextAsset definition;

    // Set at runtime (e.g. from a clone or a string passed to StartMonitoring). Takes priority over the TextAsset.
    [NonSerialized] public string definitionText;

    public MonitorDuple(string capabilityID, GameObject gameObject)
    {
        CapabilityID = capabilityID;
        this.gameObject = gameObject;
    }
}

public class CapabilityStatus
{
    public string CapabilityID;
    public JObject Capability;           // Last status successfully sent for this capability
    public bool Published;               // Definition accepted by the gateway; status updates allowed
    public bool RequestInFlight;         // Prevents overlapping requests
    public float NextAllowedTime;        // Throttling and retry backoff
}

public class CapabilityMonitor : MonoBehaviour
{
    [SerializeField] private string _gatewayUrl = "https://www.olimn.com";

    [SerializeField, Tooltip("Dev only. Leave empty to use the CLI's saved token.")]
    private string _accessToken;

    [SerializeField, Tooltip("Minimum seconds between status updates for a single capability.")]
    private float _minSendInterval = 0.5f;

    [SerializeField, Tooltip("Seconds to wait before retrying after a failed request.")]
    private float _retryDelay = 5f;

    [SerializeField] private List<MonitorDuple> _monitorDuples = new List<MonitorDuple>();

    private CapabilityNetwork _capabilityNetwork;
    private readonly List<CapabilityStatus> _capabilityStatus = new List<CapabilityStatus>();
    private CancellationTokenSource _cts;

    void Awake()
    {
        _cts = new CancellationTokenSource();
        _capabilityNetwork = new CapabilityNetwork(_gatewayUrl,
            () => _accessToken);
    }

    void OnDestroy()
    {
        // Abort any requests still in flight, then release the HttpClient.
        _cts.Cancel();
        _capabilityNetwork.Dispose();
    }

    /// <param name="definition">Optional markdown to publish as the capability definition.
    /// Ignored when cloning (the original's definition is copied instead).</param>
    public async void StartMonitoring(string objectName, string baseCapabilityID, bool clone = false, string definition = null)
    {
        // Note: GameObject.Find only finds active objects.
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            Debug.LogWarning($"Could not find GameObject identified by \"{objectName}\"");
            return;
        }

        string capabilityID = baseCapabilityID;

        if (clone)
        {
            capabilityID = $"{baseCapabilityID}_CLONE";
            try
            {
                // Copy the original's markdown; it gets published under the new ID by Update().
                definition = await _capabilityNetwork.DescribeCapabilityAsync(baseCapabilityID, _cts.Token);
            }
            catch (Exception e)
            {
                if (!_cts.IsCancellationRequested)
                    Debug.LogError($"Failed to fetch capability \"{baseCapabilityID}\" for cloning: {e}");
                return;
            }

            // This component may have been destroyed while we were waiting on the network.
            if (this == null || target == null) return;
        }

        // Update() publishes the definition first, then starts sending status.
        _monitorDuples.Add(new MonitorDuple(capabilityID, target) { definitionText = definition });
    }

    void Update()
    {
        foreach (MonitorDuple monitor in _monitorDuples)
        {
            if (monitor.gameObject == null || string.IsNullOrEmpty(monitor.CapabilityID)) continue;

            CapabilityStatus status = GetOrCreateStatus(monitor.CapabilityID);

            // Don't stack requests, and respect throttling / retry backoff.
            if (status.RequestInFlight || Time.time < status.NextAllowedTime) continue;

            // Step 1: the capability must be defined before any status goes out.
            if (!status.Published)
            {
                PublishDefinition(status, monitor);
                continue;
            }

            // Step 2: send status whenever it changes.
            JObject current = BuildStatus(monitor.gameObject);
            if (JToken.DeepEquals(current, status.Capability)) continue;

            SendStatus(status, current);
        }
    }

    // Defines what "status" means for a monitored GameObject. Whenever this result
    // changes, it gets sent to the gateway. This is an example: replace it with
    // whatever you actually want to report.
    private JObject BuildStatus(GameObject go)
    {
        Vector3 p = go.transform.position;
        return new JObject
        {
            ["active"] = go.activeInHierarchy,
            ["position"] = new JArray(Round(p.x), Round(p.y), Round(p.z)),
        };
    }

    // Rounding keeps tiny floating-point jitter from counting as a "change".
    private static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    // Picks the definition to publish: runtime text, then the Inspector TextAsset, then a generated default.
    private static string GetDefinition(MonitorDuple monitor)
    {
        if (!string.IsNullOrEmpty(monitor.definitionText)) return monitor.definitionText;
        if (monitor.definition != null) return monitor.definition.text;
        return BuildDefaultDefinition(monitor);
    }

    // Placeholder definition. Adjust to whatever format your gateway expects.
    private static string BuildDefaultDefinition(MonitorDuple monitor)
    {
        return $"# {monitor.CapabilityID}\n\n" +
               $"Status of the Unity GameObject \"{monitor.gameObject.name}\".\n";
    }

    private async void PublishDefinition(CapabilityStatus status, MonitorDuple monitor)
    {
        string markdown = GetDefinition(monitor);
        status.RequestInFlight = true;
        try
        {
            await _capabilityNetwork.PublishCapabilityContentAsync(
                markdown, $"{status.CapabilityID}.md", status.CapabilityID, _cts.Token);
            status.Published = true;
            status.NextAllowedTime = Time.time;
        }
        catch (Exception e)
        {
            status.NextAllowedTime = Time.time + _retryDelay;
            if (!_cts.IsCancellationRequested)
                Debug.LogError($"Failed to publish capability \"{status.CapabilityID}\" (retrying in {_retryDelay}s): {e}");
        }
        finally
        {
            status.RequestInFlight = false;
        }
    }

    private async void SendStatus(CapabilityStatus status, JObject newStatus)
    {
        status.RequestInFlight = true;
        try
        {
            await _capabilityNetwork.UpdateCapabilityStatusAsync(status.CapabilityID, newStatus, _cts.Token);
            status.Capability = newStatus; // Only record it once the server has accepted it
            status.NextAllowedTime = Time.time + _minSendInterval;
        }
        catch (Exception e)
        {
            // Capability stays unchanged, so the change is resent after the retry delay.
            status.NextAllowedTime = Time.time + _retryDelay;
            if (!_cts.IsCancellationRequested)
                Debug.LogError($"Failed to update status for \"{status.CapabilityID}\" (retrying in {_retryDelay}s): {e}");
        }
        finally
        {
            status.RequestInFlight = false;
        }
    }

    private CapabilityStatus GetOrCreateStatus(string capabilityID)
    {
        // Plain loop instead of List.Find to avoid a lambda allocation every frame.
        for (int i = 0; i < _capabilityStatus.Count; i++)
        {
            if (_capabilityStatus[i].CapabilityID == capabilityID)
                return _capabilityStatus[i];
        }

        var status = new CapabilityStatus { CapabilityID = capabilityID };
        _capabilityStatus.Add(status);
        return status;
    }
}