using System.Collections.Generic;
using System.Threading;
using DGLab;
using DGLab.Protocol;
using UnityEngine;

public sealed class DGLabSampleController : MonoBehaviour
{
    [Header("WebSocket Server")]
    [SerializeField] private string serverUrl = "ws://127.0.0.1:9999";

    [Header("Simple Demo")]
    [SerializeField] private int strengthA = 10;
    [SerializeField] private int strengthB = 10;

    private DGLabClient _client;

    private void Start()
    {
        _client = new DGLabClient(serverUrl, SynchronizationContext.Current);
        _client.OnConnected += () => Debug.Log("DG-Lab connected.");
        _client.OnClosed += reason => Debug.LogWarning("DG-Lab closed: " + reason);
        _client.OnError += ex => Debug.LogError(ex);
        _client.OnMessage += msg => Debug.Log("DG-Lab msg: " + msg.type + " | " + msg.message);
        _client.Connect();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            _client.SetStrengthA(strengthA);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            _client.SetStrengthB(strengthB);
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            _client.IncreaseStrengthA();
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            _client.DecreaseStrengthA();
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            _client.IncreaseStrengthB();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            _client.DecreaseStrengthB();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            _client.ClearWaveA();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            _client.ClearWaveB();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            var wave = new List<string>
            {
                "0A0A0A0A64646464",
                "1414141464646464",
                "1E1E1E1E64646464",
                "2828282864646464",
                "3232323264646464",
                "2828282864646464",
                "1E1E1E1E64646464",
                "1414141464646464"
            };

            _client.SendWaveA(wave, 5);
        }
    }

    private void OnDestroy()
    {
        _client?.Disconnect();
    }
}
