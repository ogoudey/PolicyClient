using System;

namespace PolicyClient
{
    public abstract class BaseBehavior
    {
        public abstract bool ready { get; }
        public abstract bool HandleFrame();
        public abstract bool Initialize();
    }
}