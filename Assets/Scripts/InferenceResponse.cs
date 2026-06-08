using Byter;
namespace PolicyClient
{
    /// <summary>
    /// Incoming payload from the inference server: a float[] action sequence.
    /// </summary>
    public class InferenceResponse
    {
        // Flattened storage for Byter's class serializer.
        public float[] ActionChunkData   { get; set; } = System.Array.Empty<float>();
        public int      ActionLength      { get; set; } = 0; // floats per action
        public int      ChunkLength       { get; set; } = 0; // number of actions

        // ── Convenience accessor (not serialized) ────────────────────
        public float[][] ActionChunk
        {
            get
            {
                var result = new float[ChunkLength][];
                for (int i = 0; i < ChunkLength; i++)
                {
                    result[i] = new float[ActionLength];
                    System.Array.Copy(ActionChunkData, i * ActionLength, result[i], 0, ActionLength);
                }
                return result;
            }
            set
            {
                ChunkLength = value.Length;
                ActionLength = value.Length > 0 ? value[0].Length : 0;
                ActionChunkData = new float[ChunkLength * ActionLength];
                for (int i = 0; i < ChunkLength; i++)
                    System.Array.Copy(value[i], 0, ActionChunkData, i * ActionLength, ActionLength);
            }
        }
        public byte[] ToBytes()
        {
            var p = new Primitive();
            p.Add.Class(this);
            return p.GetBytes();
        }

        public static InferenceResponse? FromBytes(byte[] buffer)
        {
            var p = new Primitive(buffer);
            var res = p.Get.Class<InferenceResponse>();
            return p.IsValid ? res : null;
        }
    }
}