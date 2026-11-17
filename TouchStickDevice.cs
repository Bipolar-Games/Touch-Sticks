#if ENABLE_INPUT_SYSTEM && BIPOLAR_TOUCHSTICKS_INPUT_SYSTEM
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bipolar.TouchSticks
{
    /// <summary>State of <see cref="TouchStickDevice"/>: the stick deflection.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct TouchStickState : IInputStateTypeInfo
    {
        public static FourCC Format => new FourCC('T', 'S', 'T', 'K');

        [FieldOffset(0)]
        [InputControl(name = "stick", layout = "Vector2")]
        public Vector2 stick;

        public FourCC format => Format;
    }

    /// <summary>
    /// Virtual Input System device written by <see cref="VirtualJoystick"/>. Bind an action to
    /// <c>&lt;TouchStick&gt;/stick</c>, or <c>&lt;TouchStick&gt;{Usage}/stick</c> to pick one stick by its Input Usage.
    /// </summary>
    [InputControlLayout(stateType = typeof(TouchStickState), displayName = "Touch Stick")]
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class TouchStickDevice : InputDevice
    {
        public const string LayoutName = "TouchStick";

        public Vector2Control Stick { get; private set; }

        static TouchStickDevice()
        {
            RegisterLayout();
        }

        /// <summary>Makes the layout known to the Input System (also lets the Input Actions editor offer it).</summary>
        public static void RegisterLayout()
        {
            InputSystem.RegisterLayout<TouchStickDevice>(LayoutName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAtStartup()
        {
            RegisterLayout();
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            Stick = GetChildControl<Vector2Control>("stick");
        }
    }
}
#endif
