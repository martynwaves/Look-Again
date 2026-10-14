// Without a headset, spawns the XR Interaction Simulator for mouse and keyboard play.
// Also hides a few harmless log messages in the Editor, in and out of Play mode.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR.Haptics;
using Object = UnityEngine.Object;

public static class DevSetup
{
    const string SimulatorResourcePath = "XR Interaction Simulator";
    const string SimulatedControllerLayout = "XRSimulatedController";
    const float HeadsetStartTimeout = 10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static async void StartSimulatorIfNoHeadset()
    {
        if (HeadsetDisplay() is { } display)
        {
            float giveUpAt = Time.realtimeSinceStartup + HeadsetStartTimeout;
            while (!display.running && Time.realtimeSinceStartup < giveUpAt)
            {
                await Awaitable.NextFrameAsync();
                if (!Application.isPlaying) return;
            }
            if (display.running) return;
            Debug.LogWarning($"A headset was found but did not start within {HeadsetStartTimeout} seconds, so mouse and keyboard controls are on instead. Restart with the headset awake to use it.");
        }
        StartSimulator();
    }

    // Kept apart from the waiting above, since C# does not allow unsafe code in an async method
    static void StartSimulator()
    {
        GameObject prefab = Resources.Load<GameObject>(SimulatorResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"XR Interaction Simulator prefab is missing at Resources/{SimulatorResourcePath}, keyboard and mouse controls are off.");
            return;
        }
        unsafe
        {
            InputSystem.onDeviceCommand -= AnswerHapticCapabilities;
            InputSystem.onDeviceCommand += AnswerHapticCapabilities;
        }
        GameObject simulator = Object.Instantiate(prefab);
        simulator.name = prefab.name;
        Object.DontDestroyOnLoad(simulator);
    }

    // XR is initialised before the first scene loads, and OpenXR only keeps its display when it found a headset
    static UnityEngine.XR.XRDisplaySubsystem HeadsetDisplay()
    {
        List<UnityEngine.XR.XRDisplaySubsystem> displays = new();
        SubsystemManager.GetSubsystems(displays);
        return displays.Count > 0 ? displays[0] : null;
    }

    static unsafe long? AnswerHapticCapabilities(InputDevice device, InputDeviceCommand* command)
    {
        if (device.layout != SimulatedControllerLayout || command->type != default(GetHapticCapabilitiesCommand).typeStatic) return null;
        ((GetHapticCapabilitiesCommand*)command)->numChannels = 1;
        return 0;
    }

#if UNITY_EDITOR
    // SubsystemRegistration runs before XR starts, which is when OpenXR logs its line
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    [UnityEditor.InitializeOnLoadMethod]
    static void InstallLogFilter()
    {
        if (Debug.unityLogger.logHandler is LogFilter) return;
        Debug.unityLogger.logHandler = new LogFilter(Debug.unityLogger.logHandler);
    }

    class LogFilter : ILogHandler
    {
        static readonly string[] HiddenPrefixes =
        {
            "Loading OpenXR loader library at path: ",
            "To avoid interfering with point and click interactions, XRUIInputModule mouse and touch input will be disabled",
            "Error reason is 'NoSubscription'",
        };

        readonly ILogHandler inner;

        public LogFilter(ILogHandler inner) { this.inner = inner; }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (logType == LogType.Log && args.Length == 1 && args[0] is string message && IsHidden(message)) return;
            inner.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context) => inner.LogException(exception, context);

        static bool IsHidden(string message)
        {
            foreach (string prefix in HiddenPrefixes) if (message.StartsWith(prefix)) return true;
            return false;
        }
    }
#endif
}
