// YALNIZ DERLEME DENETİMİ; UNITY'NİN KENDİSİ DEĞİL.
// Compile-check stubs of the UnityEngine members used by Assets/MinikUsta/Unity, with Unity 6000.0's signatures. Empty
// bodies; never executed. Keep in sync with the Unity layer: a new Unity API there needs its exact signature here.
using System;
using System.Collections;

namespace UnityEngine
{
    public class Object
    {
        public string name { get => null; set { } }
        public HideFlags hideFlags { get => default; set { } }

        public static void Destroy(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }

        public static bool operator ==(Object x, Object y) => ReferenceEquals(x, y);
        public static bool operator !=(Object x, Object y) => !ReferenceEquals(x, y);
        public static implicit operator bool(Object exists) => !ReferenceEquals(exists, null);

        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => 0;
        public override string ToString() => null;
    }

    [Flags]
    public enum HideFlags
    {
        None = 0,
        HideInHierarchy = 1,
        HideInInspector = 2,
        DontSaveInEditor = 4,
        NotEditable = 8,
        DontSaveInBuild = 16,
        DontUnloadUnusedAsset = 32,
        DontSave = 52,
        HideAndDontSave = 61,
    }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
    }

    public class Behaviour : Component
    {
        public bool enabled { get => false; set { } }
        public bool isActiveAndEnabled => false;
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public sealed class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }

        public Transform transform => null;
        public bool activeSelf => false;
        public int layer { get => 0; set { } }

        public T AddComponent<T>() where T : Component => default;
        public T GetComponent<T>() => default;
        public void SetActive(bool value) { }
    }

    public class Transform : Component, IEnumerable
    {
        protected Transform() { }

        public Vector3 position { get => default; set { } }
        public Vector3 localPosition { get => default; set { } }
        public Vector3 localScale { get => default; set { } }
        public Vector3 localEulerAngles { get => default; set { } }
        public Quaternion rotation { get => default; set { } }
        public Quaternion localRotation { get => default; set { } }
        public Transform parent { get => null; set { } }
        public int childCount => 0;

        public void SetParent(Transform p) { }
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public IEnumerator GetEnumerator() => null;
    }

    public sealed class RectTransform : Transform
    {
        public Vector2 anchorMin { get => default; set { } }
        public Vector2 anchorMax { get => default; set { } }
        public Vector2 anchoredPosition { get => default; set { } }
        public Vector2 sizeDelta { get => default; set { } }
        public Vector2 pivot { get => default; set { } }
        public Vector2 offsetMin { get => default; set { } }
        public Vector2 offsetMax { get => default; set { } }
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => default;
        public static Vector2 one => default;

        public static Vector2 operator +(Vector2 a, Vector2 b) => default;
        public static Vector2 operator -(Vector2 a, Vector2 b) => default;
        public static Vector2 operator *(Vector2 a, float d) => default;

        public static implicit operator Vector3(Vector2 v) => default;
        public static implicit operator Vector2(Vector3 v) => default;
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3(float x, float y)
        {
            this.x = x;
            this.y = y;
            z = 0f;
        }

        public static Vector3 zero => default;
        public static Vector3 one => default;

        public static Vector3 operator +(Vector3 a, Vector3 b) => default;
        public static Vector3 operator -(Vector3 a, Vector3 b) => default;
        public static Vector3 operator *(Vector3 a, float d) => default;
    }

    public struct Vector4
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    public struct Quaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            a = 1f;
        }

        public static Color white => default;
        public static Color black => default;
        public static Color clear => default;
    }

    public struct Color32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { }

        public float x { get => 0f; set { } }
        public float y { get => 0f; set { } }
        public float width { get => 0f; set { } }
        public float height { get => 0f; set { } }
    }

    public struct Mathf
    {
        public const float PI = 3.14159274f;

        public static float Min(float a, float b) => 0f;
        public static float Max(float a, float b) => 0f;
        public static float Clamp(float value, float min, float max) => 0f;
        public static float Clamp01(float value) => 0f;
        public static float Abs(float f) => 0f;
    }

    public class Time
    {
        public static float deltaTime => 0f;
        public static float unscaledDeltaTime => 0f;
        public static float time => 0f;
        /// <summary>UnityCsReference Runtime/Export/Time/Time.bindings.cs: `public static extern float realtimeSinceStartup { get; }` (B2 decode timing).</summary>
        public static float realtimeSinceStartup => 0f;
    }

    public sealed class Screen
    {
        public static int width => 0;
        public static int height => 0;
        /// <summary>UnityCsReference 6000.0 Runtime/Export/Screen/Screen.bindings.cs: `public static Rect safeArea { get; }` (screen px, origin bottom-left; U2 safe area).</summary>
        public static Rect safeArea => default;
    }

    public enum RuntimePlatform
    {
        OSXEditor = 0,
        OSXPlayer = 1,
        WindowsPlayer = 2,
        WindowsEditor = 7,
        IPhonePlayer = 8,
        Android = 11,
        LinuxPlayer = 13,
        LinuxEditor = 16,
        WebGLPlayer = 17,
    }

    public class Application
    {
        public static int targetFrameRate { get => 0; set { } }
        public static RuntimePlatform platform => default;
        public static bool isEditor => false;
        /// <summary>UnityCsReference 6000.0 Runtime/Export/Application/Application.bindings.cs: `extern public static string absoluteURL { get; }` (the page URL on WebGL, empty elsewhere).</summary>
        public static string absoluteURL => "";
        /// <summary>UnityCsReference 6000.0 Runtime/Export/Application/Application.bindings.cs: `public static void Quit(int exitCode)` and `public static void Quit()` (U2 quit confirmation).</summary>
        public static void Quit() { }
        public static void Quit(int exitCode) { }
    }

#if UNITY_ANDROID || UNITY_IOS
    /// <summary>
    /// Handheld.Vibrate() — Unity: `public static extern void Vibrate()`, UnityCsReference 6000.0 Runtime/Export/Handheld/Handheld.bindings.cs.
    /// The source carries no `#if`, but the native player build compiles the class only for mobile targets (Android, iOS): in a WebGL or
    /// desktop player `Handheld` does not exist (P3-01, Unity Issue Tracker 1332218 "Standalone: Handheld does not exist"). So the stub
    /// exists only when UNITY_ANDROID or UNITY_IOS is defined; the default dotnet build (no symbol, the same as WebGL) fails on any
    /// unguarded use, and dotnet/MinikUsta.Unity.Android compiles the guarded branch.
    /// </summary>
    public class Handheld
    {
        public static void Vibrate() { }
    }
#endif

    public class Debug
    {
        public static bool isDebugBuild => false;
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    public enum TouchPhase
    {
        Began = 0,
        Moved = 1,
        Stationary = 2,
        Ended = 3,
        Canceled = 4,
    }

    public struct Touch
    {
        public int fingerId { get => 0; set { } }
        public Vector2 position { get => default; set { } }
        public TouchPhase phase { get => default; set { } }
    }

    public enum KeyCode
    {
        None = 0,
        /// <summary>UnityCsReference Runtime/Export/Input/KeyCode.cs: `Escape = 27` (the Android back key in the legacy Input Manager; U2).</summary>
        Escape = 27,
        A = 97,
        N = 110,
        R = 114,
        S = 115,
    }

    public class Input
    {
        public static int touchCount => 0;
        public static Vector3 mousePosition => default;
        public static bool simulateMouseWithTouches { get => false; set { } }

        public static Touch GetTouch(int index) => default;
        public static bool GetMouseButton(int button) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;
        public static bool GetKeyDown(KeyCode key) => false;
    }

    public enum CameraClearFlags
    {
        Skybox = 1,
        Color = 2,
        SolidColor = 2,
        Depth = 3,
        Nothing = 4,
    }

    public sealed class Camera : Behaviour
    {
        public static Camera main => null;

        public bool orthographic { get => false; set { } }
        public float orthographicSize { get => 0f; set { } }
        public CameraClearFlags clearFlags { get => default; set { } }
        public Color backgroundColor { get => default; set { } }
    }

    public class Renderer : Component
    {
        public bool enabled { get => false; set { } }
        public int sortingOrder { get => 0; set { } }
    }

    public enum SpriteDrawMode
    {
        Simple = 0,
        Sliced = 1,
        Tiled = 2,
    }

    public sealed class SpriteRenderer : Renderer
    {
        public Sprite sprite { get => null; set { } }
        public Color color { get => default; set { } }
        public SpriteDrawMode drawMode { get => default; set { } }
        public Vector2 size { get => default; set { } }
        public bool flipX { get => false; set { } }
        public bool flipY { get => false; set { } }
    }

    public enum TextureFormat
    {
        Alpha8 = 1,
        RGB24 = 3,
        RGBA32 = 4,
        ARGB32 = 5,
    }

    public enum FilterMode
    {
        Point = 0,
        Bilinear = 1,
        Trilinear = 2,
    }

    public enum TextureWrapMode
    {
        Repeat = 0,
        Clamp = 1,
        Mirror = 2,
        MirrorOnce = 3,
    }

    public class Texture : Object
    {
        public virtual int width { get => 0; set { } }
        public virtual int height { get => 0; set { } }
        public FilterMode filterMode { get => default; set { } }
        public TextureWrapMode wrapMode { get => default; set { } }
    }

    public sealed class Texture2D : Texture
    {
        public Texture2D(int width, int height) { }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { }

        public void SetPixels32(Color32[] colors) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        public void Apply(bool updateMipmaps) { }
        public void Apply() { }
    }

    public enum SpriteMeshType
    {
        FullRect = 0,
        Tight = 1,
    }

    public sealed class Sprite : Object
    {
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border, bool generateFallbackPhysicsShape) => null;

        public float pixelsPerUnit => 0f;
        public Vector4 border => default;
    }

    // UnityCsReference 6000.0.85f1 Runtime/Export/Scripting/TextAsset.cs, TextAsset.bindings.cs
    public partial class TextAsset : Object
    {
        public string text => null;
        public byte[] bytes => null;
    }

    // UnityCsReference 6000.0.85f1 Modules/ImageConversion/ScriptBindings/ImageConversion.bindings.cs (ImageConversionModule).
    // The ReadOnlySpan<byte> overloads exist too; the Unity layer calls the byte[] ones.
    public static class ImageConversion
    {
        public static bool LoadImage(this Texture2D tex, byte[] data, bool markNonReadable) => false;
        public static bool LoadImage(this Texture2D tex, byte[] data) => false;
    }

    // UnityCsReference 6000.0.85f1 Runtime/Export/PlayerPrefs/PlayerPrefs.bindings.cs
    public class PlayerPrefs
    {
        public static void SetString(string key, string value) { }
        public static string GetString(string key, string defaultValue) => null;
        public static string GetString(string key) => null;
        public static bool HasKey(string key) => false;
        public static void DeleteKey(string key) { }
        public static void Save() { }
    }

    // UnityCsReference 6000.0.85f1 Modules/Audio/Public/ScriptBindings/Audio.bindings.cs (AudioModule). AudioClip derives from the
    // internal-ish AudioResource there; the stub keeps Object (nothing in the Unity layer needs the base).
    public class AudioBehaviour : Behaviour
    {
    }

    public sealed class AudioListener : AudioBehaviour
    {
    }

    public sealed class AudioClip : Object
    {
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => null;
        public bool SetData(float[] data, int offsetSamples) => false;
    }

    public sealed class AudioSource : AudioBehaviour
    {
        public bool playOnAwake { get => false; set { } }
        public float spatialBlend { get => 0f; set { } }
        public void PlayOneShot(AudioClip clip) { }
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
    }

    public enum FontStyle
    {
        Normal = 0,
        Bold = 1,
        Italic = 2,
        BoldAndItalic = 3,
    }

    public enum TextAnchor
    {
        UpperLeft = 0,
        UpperCenter = 1,
        UpperRight = 2,
        MiddleLeft = 3,
        MiddleCenter = 4,
        MiddleRight = 5,
        LowerLeft = 6,
        LowerCenter = 7,
        LowerRight = 8,
    }

    public enum HorizontalWrapMode
    {
        Wrap = 0,
        Overflow = 1,
    }

    public enum VerticalWrapMode
    {
        Truncate = 0,
        Overflow = 1,
    }

    public sealed class Font : Object
    {
    }

    public sealed class Resources
    {
        public static T Load<T>(string path) where T : Object => default;
        public static T GetBuiltinResource<T>(string path) where T : Object => default;
        /// <summary>UnityCsReference Runtime/Export/Resources/Resources.bindings.cs: `public static void UnloadAsset(Object assetToUnload)` (B2: the PNG bytes of a decoded page).</summary>
        public static void UnloadAsset(Object assetToUnload) { }
    }

    public enum RenderMode
    {
        ScreenSpaceOverlay = 0,
        ScreenSpaceCamera = 1,
        WorldSpace = 2,
    }

    public sealed class Canvas : Behaviour
    {
        public RenderMode renderMode { get => default; set { } }
        public int sortingOrder { get => 0; set { } }
        public float scaleFactor { get => 0f; set { } }
    }

    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad = 0,
        BeforeSceneLoad = 1,
        AfterAssembliesLoaded = 2,
        BeforeSplashScreen = 3,
        SubsystemRegistration = 4,
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RuntimeInitializeOnLoadMethodAttribute : Scripting.PreserveAttribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }

        public RuntimeInitializeLoadType loadType { get => default; set { } }
    }

    // Name-collision guards: real UnityEngine types whose names also exist in MinikUsta.Core / Presentation. With
    // `using UnityEngine;` and `using MinikUsta.Core;` in one file, an unqualified `Grid` is ambiguous in Unity too.
    public class GridLayout : Behaviour
    {
    }

    public sealed class Grid : GridLayout
    {
    }

    public struct Pose
    {
    }

    /// <summary>GUI.DrawTexture scale mode; collides with MinikUsta.Presentation.Layout.ScaleMode.</summary>
    public enum ScaleMode
    {
        StretchToFill = 0,
        ScaleAndCrop = 1,
        ScaleToFit = 2,
    }

    public class Motion : Object
    {
    }
}

namespace UnityEngine.Scripting
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor
        | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface
        | AttributeTargets.Delegate, Inherited = false)]
    public class PreserveAttribute : Attribute
    {
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name => null;
        public string path => null;
        public bool isLoaded => false;
        public int buildIndex => 0;
    }

    public class SceneManager
    {
        public static Scene GetActiveScene() => default;
    }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();

    public abstract class UnityEventBase
    {
        public void RemoveAllListeners() { }
    }

    public class UnityEvent : UnityEventBase
    {
        public void AddListener(UnityAction call) { }
        public void RemoveListener(UnityAction call) { }
        public void Invoke() { }
    }
}

namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : MonoBehaviour
    {
    }

    public class EventSystem : UIBehaviour
    {
        public static EventSystem current { get => null; set { } }
    }

    public abstract class BaseInputModule : UIBehaviour
    {
    }

    public abstract class PointerInputModule : BaseInputModule
    {
    }

    public class StandaloneInputModule : PointerInputModule
    {
    }

    public abstract class BaseRaycaster : UIBehaviour
    {
    }

    // com.unity.ugui 2.0 Runtime/EventSystem/EventInterfaces.cs, EventData/*.cs
    public abstract class AbstractEventData
    {
    }

    public class BaseEventData : AbstractEventData
    {
    }

    public class PointerEventData : BaseEventData
    {
    }

    public interface IEventSystemHandler
    {
    }

    public interface IPointerExitHandler : IEventSystemHandler
    {
        void OnPointerExit(PointerEventData eventData);
    }

    public interface IPointerDownHandler : IEventSystemHandler
    {
        void OnPointerDown(PointerEventData eventData);
    }

    public interface IPointerUpHandler : IEventSystemHandler
    {
        void OnPointerUp(PointerEventData eventData);
    }
}

namespace UnityEngine.UI
{
    using UnityEngine.Events;
    using UnityEngine.EventSystems;

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode
        {
            ConstantPixelSize = 0,
            ScaleWithScreenSize = 1,
            ConstantPhysicalSize = 2,
        }

        public enum ScreenMatchMode
        {
            MatchWidthOrHeight = 0,
            Expand = 1,
            Shrink = 2,
        }

        public ScaleMode uiScaleMode { get => default; set { } }
        public Vector2 referenceResolution { get => default; set { } }
        public ScreenMatchMode screenMatchMode { get => default; set { } }
        public float matchWidthOrHeight { get => 0f; set { } }
        public float referencePixelsPerUnit { get => 0f; set { } }
        public float scaleFactor { get => 0f; set { } }
    }

    public class GraphicRaycaster : BaseRaycaster
    {
    }

    public abstract class Graphic : UIBehaviour
    {
        public virtual Color color { get => default; set { } }
        public virtual bool raycastTarget { get => false; set { } }
        public RectTransform rectTransform => null;
    }

    public abstract class MaskableGraphic : Graphic
    {
    }

    public class Image : MaskableGraphic
    {
        public enum Type
        {
            Simple = 0,
            Sliced = 1,
            Tiled = 2,
            Filled = 3,
        }

        public Sprite sprite { get => null; set { } }
        public Type type { get => default; set { } }
        public bool preserveAspect { get => false; set { } }
    }

    public class Text : MaskableGraphic
    {
        public virtual string text { get => null; set { } }
        public Font font { get => null; set { } }
        public int fontSize { get => 0; set { } }
        public FontStyle fontStyle { get => default; set { } }
        public TextAnchor alignment { get => default; set { } }
        public HorizontalWrapMode horizontalOverflow { get => default; set { } }
        public VerticalWrapMode verticalOverflow { get => default; set { } }
        public bool resizeTextForBestFit { get => false; set { } }
        public int resizeTextMinSize { get => 0; set { } }
        public int resizeTextMaxSize { get => 0; set { } }
        public bool supportRichText { get => false; set { } }
    }

    // com.unity.ugui Runtime/UI/Core/VertexModifiers/BaseMeshEffect.cs, Shadow.cs, Outline.cs (IMeshModifier omitted: not used)
    public abstract class BaseMeshEffect : UIBehaviour
    {
    }

    public class Shadow : BaseMeshEffect
    {
        protected Shadow() { }

        public Color effectColor { get => default; set { } }
        public Vector2 effectDistance { get => default; set { } }
        public bool useGraphicAlpha { get => false; set { } }
    }

    public class Outline : Shadow
    {
        protected Outline() { }
    }

    public class Selectable : UIBehaviour
    {
        // com.unity.ugui Runtime/UI/Core/Selectable.cs
        public enum Transition
        {
            None = 0,
            ColorTint = 1,
            SpriteSwap = 2,
            Animation = 3,
        }

        public Transition transition { get => default; set { } }
        public Graphic targetGraphic { get => null; set { } }
        public bool interactable { get => false; set { } }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent
        {
        }

        public ButtonClickedEvent onClick { get => null; set { } }
    }
}
