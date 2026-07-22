using System;
using System.Runtime.InteropServices;
using AOT;
using UnityAngularBridge;
using UnityEngine;

/// <summary>
/// Example MonoBehaviour that demonstrates all communication patterns
/// of the Unity-Angular-Bridge.
///
/// 1. Angular → Unity:  Methods marked with [AngularExposed] can be called
///    from Angular via the auto-generated UnityClient.ts.
/// 2. Unity → Angular:  Methods marked with [DllImport("__Internal")] push
///    data to Angular via the auto-generated UnityJSLibExportedService.
/// 3. Callbacks:         Request-response and event registration patterns
///    for bidirectional async communication.
/// </summary>
public class SceneManager : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────
    //  JSON DTOs
    //  Referenced via JsonType on the attributes below; the generators emit
    //  matching TypeScript interfaces for them.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scene state snapshot sent to Angular as JSON.
    /// </summary>
    [Serializable]
    public class SceneState
    {
        public string selectedObjectId;
        public int objectCount;
        public bool visible;
    }

    /// <summary>
    /// Spawn request received from Angular as JSON.
    /// </summary>
    [Serializable]
    public class SpawnRequest
    {
        public string objectId;
        public float x;
        public float y;
        public float z;
        public string colorHex;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Unity → Angular  (JSLib / DllImport)
    //  These generate BrowserInteractions.jslib + unity-jslib-exported.service.ts
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends the currently selected object ID to Angular.
    /// </summary>
    [DllImport("__Internal")]
    [JSLibExport(Category = "Selection")]
    private static extern void SendSelectedObject(string objectId);

    /// <summary>
    /// Notifies Angular that the scene has finished loading.
    /// </summary>
    [DllImport("__Internal")]
    [JSLibExport(Category = "Lifecycle")]
    private static extern void SendSceneReady();

    /// <summary>
    /// Sends a pipe-delimited list of object IDs to Angular (split into string[]).
    /// </summary>
    [DllImport("__Internal")]
    [JSLibExport(IsStringArray = true, Category = "Objects")]
    private static extern void SendObjectsList(string objectIds);

    /// <summary>
    /// Sends the current scene state to Angular as a typed JSON object.
    /// </summary>
    [DllImport("__Internal")]
    [JSLibExport(JsonType = typeof(SceneState), Category = "State")]
    private static extern void SendSceneState(string json);

    // ──────────────────────────────────────────────────────────────────
    //  Callbacks  (Request-Response & Event Registration)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests data from the web page. Angular processes the query and responds via callback.
    /// </summary>
    [DllImport("__Internal")]
    [JSLibExport(Category = "Data")]
    private static extern void RequestDataFromWeb(string query, Action<string> onResult);

    /// <summary>
    /// Registers a callback that Angular can invoke to notify Unity of navigation changes.
    /// </summary>
    [DllImport("__Internal")]
    [JSLibExport(IsCallbackRegistration = true, Category = "Navigation")]
    private static extern void RegisterOnNavigationChanged(Action<string> handler);

    [MonoPInvokeCallback(typeof(Action<string>))]
    private static void OnDataReceived(string data)
    {
        Debug.Log($"[SceneManager] Data received from web: {data}");
    }

    [MonoPInvokeCallback(typeof(Action<string>))]
    private static void OnNavigationChanged(string route)
    {
        Debug.Log($"[SceneManager] Navigation changed to: {route}");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Scene objects
    // ──────────────────────────────────────────────────────────────────

    private readonly System.Collections.Generic.Dictionary<string, GameObject> _objects
        = new System.Collections.Generic.Dictionary<string, GameObject>();

    private string _selectedObjectId;
    private bool _visible = true;

    // ──────────────────────────────────────────────────────────────────
    //  Angular → Unity  (AngularExposed)
    //  These generate UnityClient.ts
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Load an object by its ID. Called from Angular.
    /// </summary>
    [AngularExposed(gameObjectName: "SceneManager")]
    public void LoadObject(string objectId)
    {
        Debug.Log($"[SceneManager] LoadObject({objectId})");

        // Deselect previous
        if (_selectedObjectId != null && _objects.ContainsKey(_selectedObjectId))
        {
            HighlightObject(_objects[_selectedObjectId], false);
        }

        // Select new
        _selectedObjectId = objectId;
        if (_objects.ContainsKey(objectId))
        {
            HighlightObject(_objects[objectId], true);
        }

#if PLATFORM_WEBGL && !UNITY_EDITOR
        SendSelectedObject(objectId);
#endif
    }

    /// <summary>
    /// Set the color of the selected object. Called from Angular.
    /// </summary>
    [AngularExposed(gameObjectName: "SceneManager")]
    public void SetColor(string colorHex)
    {
        Debug.Log($"[SceneManager] SetColor({colorHex})");

        if (_selectedObjectId != null
            && _objects.ContainsKey(_selectedObjectId)
            && ColorUtility.TryParseHtmlString(colorHex, out Color color))
        {
            var renderer = _objects[_selectedObjectId].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }
    }

    /// <summary>
    /// Toggle visibility of all objects. Called from Angular.
    /// </summary>
    [AngularExposed(gameObjectName: "SceneManager")]
    public void ToggleVisibility()
    {
        Debug.Log("[SceneManager] ToggleVisibility()");
        _visible = !_visible;
        foreach (var obj in _objects.Values)
        {
            obj.SetActive(_visible);
        }
        PublishSceneState();
    }

    /// <summary>
    /// Spawn a new object from a typed JSON request. Called from Angular.
    /// </summary>
    [AngularExposed(gameObjectName: "SceneManager", JsonType = typeof(SpawnRequest))]
    public void SpawnFromJson(string request)
    {
        SpawnRequest spawnRequest = JsonUtility.FromJson<SpawnRequest>(request);
        Debug.Log($"[SceneManager] SpawnFromJson({spawnRequest.objectId})");

        SpawnObject(spawnRequest.objectId, PrimitiveType.Cube, new Vector3(spawnRequest.x, spawnRequest.y, spawnRequest.z));

        if (!string.IsNullOrEmpty(spawnRequest.colorHex)
            && ColorUtility.TryParseHtmlString(spawnRequest.colorHex, out Color color))
        {
            var renderer = _objects[spawnRequest.objectId].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        PublishSceneState();
    }

    /// <summary>
    /// Reset the scene to its initial state. Called from Angular.
    /// </summary>
    [AngularExposed(gameObjectName: "SceneManager")]
    public void ResetScene()
    {
        Debug.Log("[SceneManager] ResetScene()");

        // Destroy existing objects
        foreach (var obj in _objects.Values)
        {
            Destroy(obj);
        }
        _objects.Clear();
        _selectedObjectId = null;
        _visible = true;

        // Spawn the demo primitives in front of the camera
        SpawnObject("Cube-001",     PrimitiveType.Cube,     new Vector3(-3f, 1f, 0f));
        SpawnObject("Sphere-002",   PrimitiveType.Sphere,   new Vector3(-1f, 1f, 0f));
        SpawnObject("Cylinder-003", PrimitiveType.Cylinder,  new Vector3(1f, 1f, 0f));
        SpawnObject("Plane-004",    PrimitiveType.Quad,      new Vector3(3f, 1f, 0f));

        string[] objectIds = { "Cube-001", "Sphere-002", "Cylinder-003", "Plane-004" };

#if PLATFORM_WEBGL && !UNITY_EDITOR
        SendObjectsList(string.Join("|", objectIds));
        SendSceneReady();
        RequestDataFromWeb("scene-reset", OnDataReceived);
#endif
        PublishSceneState();
    }

    private void Start()
    {
        ResetScene();

#if PLATFORM_WEBGL && !UNITY_EDITOR
        SendSceneReady();
        RegisterOnNavigationChanged(OnNavigationChanged);
#endif
    }

    // ──────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────

    private void SpawnObject(string id, PrimitiveType type, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = id;
        go.transform.position = position;

        // Remove collider — physics may be stripped in WebGL builds
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Use a built-in unlit shader so it doesn't go pink in WebGL
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("UI/Default"));
            renderer.material.color = Color.white;
        }

        _objects[id] = go;
    }

    /// <summary>
    /// Push a JSON snapshot of the scene state to Angular.
    /// </summary>
    private void PublishSceneState()
    {
#if PLATFORM_WEBGL && !UNITY_EDITOR
        SceneState state = new SceneState
        {
            selectedObjectId = _selectedObjectId ?? string.Empty,
            objectCount = _objects.Count,
            visible = _visible,
        };
        SendSceneState(JsonUtility.ToJson(state));
#endif
    }

    private void HighlightObject(GameObject go, bool selected)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.material.color = selected ? Color.yellow : Color.white;
    }
}
