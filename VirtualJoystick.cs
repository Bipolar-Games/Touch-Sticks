using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bipolar.TouchSticks
{
    /// <summary>
    /// One on-screen joystick (drawn with IMGUI, no Canvas needed). Add one component per stick.
    /// A hidden updater, created at runtime when the first stick is enabled, drives and draws all sticks,
    /// so this component has no Update or OnGUI of its own.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour
    {
        /// <summary>Raised when a finger starts controlling this stick.</summary>
        public event Action OnPressed;

        /// <summary>Raised when the finger controlling this stick is lifted (or the stick is disabled).</summary>
        public event Action OnReleased;

        private const int None = int.MinValue;
        private const int TextureSize = 128;
        private const float RingThickness = 6f;

        [Header("Layout (screen fractions, origin bottom-left)")]
        [Tooltip("Area where a new touch can grab this stick.")]
        [SerializeField]
        private Rect captureZone = new Rect(0f, 0f, 0.5f, 1f);

        [Tooltip("Resting position inside the screen reduced by the margin: (0,0) bottom-left corner, (1,1) top-right.")]
        [SerializeField]
        private Vector2 idleAnchor = Vector2.zero;

        [Tooltip("Stick radius as a fraction of the screen height.")]
        [SerializeField]
        private float radiusScreenHeight = 0.13f;

        [SerializeField]
        private float minRadiusPixels = 60f;

        [Tooltip("Distance of the resting stick from the screen edge, as a fraction of the screen height.")]
        [SerializeField]
        private float marginScreenHeight = 0.05f;

        [SerializeField]
        private float minMarginPixels = 20f;

        [Header("Behaviour")]
        [Range(0f, 1f)]
        [SerializeField]
        private float deadZone = 0.12f;

        [Tooltip("Cut: the value jumps from 0 to the dead zone size. Rescale: the value grows smoothly from 0 at the dead zone edge. Ignored when Normalize Output is on.")]
        [SerializeField]
        private DeadZoneMode deadZoneMode = DeadZoneMode.Cut;
        public DeadZoneMode DeadZoneMode
        {
            get => deadZoneMode;
            set => deadZoneMode = value;
        }

        [Tooltip("Value always has length 1 (or 0 inside the dead zone) instead of 0..1.")]
        [SerializeField]
        private bool normalizeOutput;

        [Tooltip("The stick base appears where the finger lands. Otherwise it stays at the resting position.")]
        [SerializeField]
        private bool floating = true;

        [Tooltip("When the finger leaves the radius, the stick base follows it.")]
        [SerializeField]
        private bool followFinger = true;

        [Header("Look")]
        [SerializeField]
        private Color color = Color.white;

        [Tooltip("When false the stick works as usual but is not drawn.")]
        [SerializeField]
        private bool visible = true;
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        [Header("States")]
        [SerializeField]
        private int pointerId = None;

        private static readonly List<VirtualJoystick> sticks = new List<VirtualJoystick>();
        private static Updater updater;
        private static Texture2D ringTexture;
        private static Texture2D discTexture;

        private Vector2 origin;
        private Vector2 current;

        /// <summary>Deflection after the dead zone: length 0..1, or exactly 1 when output is normalized. See <see cref="DeadZoneMode"/>.</summary>
        public Vector2 Value { get; private set; }

        /// <summary>True while a finger controls this stick.</summary>
        public bool IsPressed => pointerId != None;

        /// <summary>When false the stick ignores new touches, drops the current one and is not drawn.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Color of the stick; alpha is controlled by the stick state.</summary>
        public Color Color
        {
            get => color;
            set => color = value;
        }

        private float Radius => Mathf.Max(minRadiusPixels, Screen.height * radiusScreenHeight);

        /// <summary>Sets the main parameters from code; use when the component is added at runtime.</summary>
        public void Configure(Rect newCaptureZone, Vector2 newIdleAnchor, float newDeadZone, bool newNormalizeOutput)
        {
            captureZone = newCaptureZone;
            idleAnchor = newIdleAnchor;
            deadZone = newDeadZone;
            normalizeOutput = newNormalizeOutput;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sticks.Clear();
            updater = null;
            ringTexture = null;
            discTexture = null;
        }

        private void OnEnable()
        {
            if (TouchPointers.Enabled == false)
                return;

            if (updater == null)
                updater = CreateUpdater();

            sticks.Add(this);
        }

        private void OnDisable()
        {
            sticks.Remove(this);
            ReleasePointer();
        }

        private static Updater CreateUpdater()
        {
            var host = new GameObject("TouchSticks Updater") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(host);
            return host.AddComponent<Updater>();
        }

        private void Tick()
        {
            var pointers = TouchPointers.Pointers;
            if (IsPressed)
                FollowPointer(pointers);

            if (IsPressed == false)
                TryGrabPointer(pointers);

            UpdateValue();
        }

        private void FollowPointer(IReadOnlyList<Pointer> pointers)
        {
            if (InputEnabled == false)
            {
                ReleasePointer();
                return;
            }

            for (int i = 0; i < pointers.Count; i++)
            {
                if (pointers[i].Id != pointerId)
                    continue;

                if (pointers[i].Ended)
                    ReleasePointer();
                else
                    current = pointers[i].Position;
                return;
            }

            ReleasePointer();
        }

        private void TryGrabPointer(IReadOnlyList<Pointer> pointers)
        {
            if (InputEnabled == false)
                return;

            for (int i = 0; i < pointers.Count; i++)
            {
                var pointer = pointers[i];
                if (pointer.Began == false || pointer.Ended)
                    continue;

                var normalized = new Vector2(pointer.Position.x / Screen.width, pointer.Position.y / Screen.height);
                if (captureZone.Contains(normalized) == false)
                    continue;

                if (TouchPointers.TryClaim(pointer.Id) == false)
                    continue;

                pointerId = pointer.Id;
                origin = floating ? pointer.Position : IdleCenter();
                current = pointer.Position;
                OnPressed?.Invoke();
                return;
            }
        }

        private void ReleasePointer()
        {
            if (IsPressed == false)
                return;

            TouchPointers.Release(pointerId);
            pointerId = None;
            Value = Vector2.zero;
            OnReleased?.Invoke();
        }

        private void UpdateValue()
        {
            if (IsPressed == false)
            {
                Value = Vector2.zero;
                return;
            }

            float radius = Radius;
            var deflection = (current - origin) / radius;
            if (deflection.magnitude > 1f)
            {
                deflection.Normalize();
                if (followFinger)
                    origin = current - deflection * radius;
            }

            Value = ApplyDeadZone(deflection);
        }

        private Vector2 ApplyDeadZone(Vector2 deflection)
        {
            float magnitude = deflection.magnitude;
            if (magnitude < deadZone)
                return Vector2.zero;

            var direction = deflection.normalized;
            if (normalizeOutput)
                return direction;

            if (deadZoneMode == DeadZoneMode.Cut)
                return deflection;

            return direction * Mathf.InverseLerp(deadZone, 1f, magnitude);
        }

        private Vector2 IdleCenter()
        {
            float radius = Radius;
            float margin = Mathf.Max(minMarginPixels, Screen.height * marginScreenHeight) + radius;
            var min = new Vector2(margin, margin);
            var max = new Vector2(Screen.width - margin, Screen.height - margin);
            return new Vector2(Mathf.Lerp(min.x, max.x, idleAnchor.x), Mathf.Lerp(min.y, max.y, idleAnchor.y));
        }

        private void Draw()
        {
            float radius = Radius;
            var center = IsPressed ? origin : IdleCenter();
            var knob = IsPressed ? origin + Vector2.ClampMagnitude(current - origin, radius) : center;

            GUI.color = new Color(color.r, color.g, color.b, IsPressed ? 0.85f : 0.35f);
            GUI.DrawTexture(Square(center, radius * 2f), ringTexture);
            GUI.color = new Color(color.r, color.g, color.b, IsPressed ? 0.9f : 0.3f);
            GUI.DrawTexture(Square(knob, radius * 0.8f), discTexture);
            GUI.color = Color.white;
        }

        private static Rect Square(Vector2 screenCenter, float size)
        {
            return new Rect(screenCenter.x - size * 0.5f, Screen.height - screenCenter.y - size * 0.5f, size, size);
        }

        private static void EnsureTextures()
        {
            if (ringTexture == null)
                ringTexture = MakeTexture(RingThickness);

            if (discTexture == null)
                discTexture = MakeTexture(0f);
        }

        /// <summary>Disc (thickness 0) or ring of the given pixel thickness, with a smooth edge.</summary>
        private static Texture2D MakeTexture(float thickness)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var middle = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
            float outer = TextureSize * 0.5f - 1f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), middle);
                    float alpha = Mathf.Clamp01(outer - distance);
                    if (thickness > 0f)
                        alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - (outer - thickness)));

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>Runtime singleton that updates and draws every enabled stick. Nested and private, so it cannot be added in the editor.</summary>
        [DefaultExecutionOrder(-100)]
        private sealed class Updater : MonoBehaviour
        {
            private void Update()
            {
                TouchPointers.Gather();
                for (int i = 0; i < sticks.Count; i++)
                    sticks[i].Tick();
            }

            private void OnGUI()
            {
                if (Event.current.type != EventType.Repaint)
                    return;

                EnsureTextures();
                for (int i = 0; i < sticks.Count; i++)
                    if (sticks[i].InputEnabled && sticks[i].Visible)
                        sticks[i].Draw();
            }

            private void OnDestroy()
            {
                if (ringTexture)
                    Destroy(ringTexture);

                if (discTexture)
                    Destroy(discTexture);

                ringTexture = null;
                discTexture = null;
                if (updater == this)
                    updater = null;
            }
        }
    }
}
