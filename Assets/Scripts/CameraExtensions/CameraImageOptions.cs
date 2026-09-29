using System;
using UnityEngine;

[Serializable]
public struct CustomCameraSettings : ISerializationCallbackReceiver
{
    [Tooltip("Reference to the target Camera component.")]
    public Camera camera;

    [Tooltip("Width setting for the camera view/texture.")]
    public int width;

    [Tooltip("Height setting for the camera view/texture.")]
    public int height;

    [Tooltip("Custom name for the camera. Defaults to the GameObject name if left blank.")]
    public string name;

    // Constructor to set default values when instantiated via code
    public CustomCameraSettings(Camera cam, int w = 224, int h = 224, string n = "")
    {
        camera = cam;
        width = w;
        height = h;
        name = string.IsNullOrEmpty(n) && cam != null ? cam.gameObject.name : n;
    }

    // Called before Unity serializes the data
    public void OnBeforeSerialize()
    {
        // Automatically default name to the GameObject's name if a camera is assigned and name is empty
        if (camera != null && string.IsNullOrWhiteSpace(name))
        {
            name = camera.gameObject.name;
        }
    }

    // Called after Unity deserializes the data
    public void OnAfterDeserialize() { }
}