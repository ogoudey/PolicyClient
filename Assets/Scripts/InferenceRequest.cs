using Byter;
using UnityEngine;
using System.Collections;
namespace PolicyClient
{
    /// <summary>
    /// Outgoing payload: a string command, a raw image buffer, and
    /// one or more float arrays (e.g. joint angles, sensor readings).
    /// Serialized with Byter Primitive so it can be sent over Netly TCP.
    /// </summary>
    public class InferenceRequest
    {
        // Sent as-is by Byter's class serializer.
        public string  Command         { get; set; } = string.Empty;
        public byte[]  Image           { get; set; } = System.Array.Empty<byte>();

        // Flattened representation of float[][].
        // Store lengths so the receiver can re-split the flat array.
        public int[]   FloatArrayLengths { get; set; } = System.Array.Empty<int>();
        public float[] FloatArrayData    { get; set; } = System.Array.Empty<float>();

        // ── Convenience accessor (not serialized) ────────────────────
        [System.NonSerialized] // just in case Unity's serializer sees this
        private float[][] _floatArrays;

        public float[][] FloatArrays
        {
            get
            {
                if (_floatArrays != null) return _floatArrays;
                _floatArrays = Unflatten(FloatArrayLengths, FloatArrayData);
                return _floatArrays;
            }
            set
            {
                _floatArrays = value;
                Flatten(value, out int[] lengths, out float[] data);
                FloatArrayLengths = lengths;
                FloatArrayData = data;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────
        private static void Flatten(float[][] src, out int[] lengths, out float[] data)
        {
            if (src == null || src.Length == 0)
            {
                lengths = System.Array.Empty<int>();
                data    = System.Array.Empty<float>();
                return;
            }
            lengths = new int[src.Length];
            int total = 0;
            for (int i = 0; i < src.Length; i++) { lengths[i] = src[i].Length; total += src[i].Length; }
            data = new float[total];
            int cursor = 0;
            foreach (var arr in src) { System.Array.Copy(arr, 0, data, cursor, arr.Length); cursor += arr.Length; }
        }

        private static float[][] Unflatten(int[] lengths, float[] data)
        {
            if (lengths == null || lengths.Length == 0) return System.Array.Empty<float[]>();
            var result = new float[lengths.Length][];
            int cursor = 0;
            for (int i = 0; i < lengths.Length; i++)
            {
                result[i] = new float[lengths[i]];
                System.Array.Copy(data, cursor, result[i], 0, lengths[i]);
                cursor += lengths[i];
            }
            return result;
        }

        // ── Serialization ────────────────────────────────────────────
        public byte[] ToBytes()
        {
            var p = new Primitive();
            p.Add.Class(this);
            return p.GetBytes();
        }

        public static InferenceRequest? FromBytes(byte[] buffer)
        {
            var p = new Primitive(buffer);
            var req = p.Get.Class<InferenceRequest>();
            return p.IsValid ? req : null;
        }
    }
}