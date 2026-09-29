using System;
using UnityEngine;
using System.Collections.Generic;

public static class CameraExtensions
{
    // Renders a Camera's view into a Texture2D you can read pixels from.
    // Reuses a cached RenderTexture per-camera to avoid allocating one every call.
    private static readonly Dictionary<Camera, RenderTexture> _rtCache = new();

    public static Texture2D RenderToTexture(this Camera cam, int width = 224, int height = 224)
    {
        if (!_rtCache.TryGetValue(cam, out var rt) || rt.width != width || rt.height != height)
        {
            if (rt != null) rt.Release();
            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _rtCache[cam] = rt;
        }

        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        RenderTexture.active = rt;
        cam.Render();

        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        // restore camera state so this doesn't fight with normal rendering
        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        return tex;
    }
}