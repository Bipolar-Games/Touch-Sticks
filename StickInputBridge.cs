using UnityEngine;
#if ENABLE_INPUT_SYSTEM && BIPOLAR_TOUCHSTICKS_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Bipolar.TouchSticks
{
    /// <summary>
    /// Mirrors a stick's value into a <c>TouchStick</c> Input System device so that actions can use it.
    /// Does nothing when the Input System backend is not compiled in.
    /// </summary>
    internal sealed class StickInputBridge
    {
#if ENABLE_INPUT_SYSTEM && BIPOLAR_TOUCHSTICKS_INPUT_SYSTEM
        private TouchStickDevice device;
        private Vector2 lastWritten;

        public void Open(string usage)
        {
            if (device != null)
                return;

            TouchStickDevice.RegisterLayout();
            device = InputSystem.AddDevice(TouchStickDevice.LayoutName) as TouchStickDevice;
            if (device != null && string.IsNullOrEmpty(usage) == false)
                InputSystem.SetDeviceUsage(device, usage);

            lastWritten = Vector2.zero;
        }

        public void Write(Vector2 value)
        {
            if (device == null || value == lastWritten)
                return;

            InputState.Change(device.Stick, value);
            lastWritten = value;
        }

        public void Close()
        {
            if (device == null)
                return;

            InputSystem.RemoveDevice(device);
            device = null;
        }
#else
        public void Open(string usage) { }

        public void Write(Vector2 value) { }

        public void Close() { }
#endif
    }
}
