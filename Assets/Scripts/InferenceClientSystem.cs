
using UnityEngine;
using System.Collections.Generic;
using System;
namespace PolicyClient
{
    public class InferenceClientSystem : MonoBehaviour
    {
        private static Dictionary<BehaviorType, Type> classDispatcher = new Dictionary<BehaviorType, Type>
        {
            {BehaviorType.None, null},
            {BehaviorType.ExecuteTaskBehavior, typeof(ExecuteTaskBehavior)}
        };
        private SystemState state;
        [SerializeField] private BehaviorType behaviorType = BehaviorType.None;
        [SerializeReference] private BaseBehavior behavior;  // serialized instance

        //private Type BehaviorClass => classDispatcher[behaviorType];  // lookup helper, no attribute

        void OnValidate()
        {
            if (behaviorType == BehaviorType.None)
            {
                behavior = null;
                return;
            }

            Type targetType = classDispatcher[behaviorType];

            // Only reinstantiate if the type actually changed
            if (behavior == null || behavior.GetType() != targetType)
                behavior = (BaseBehavior)Activator.CreateInstance(targetType);
        }
        void Start()
        {
            if (behaviorType == BehaviorType.None){
                Debug.LogWarning("No behavior selected!");
                return;
            }
            behavior.Initialize();
        }

        // Update is called once per frame
        void Update()
        {
            if (behavior.HandleFrame())
            {
                state = SystemState.Active;
            }
            else
            {
                Debug.LogWarning("Failed to handle frame!");
            }
        }


    }

    public enum SystemState
    {
        Initializing,
        Active
    }

    public enum BehaviorType
    {
        ExecuteTaskBehavior,
        None
    }
}

