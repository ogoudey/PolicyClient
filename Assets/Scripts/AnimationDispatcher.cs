using System;
using System.Collections.Generic;

public class AnimationDispatcher
{
    public static AnimationDispatcher Instance = new();
    public AnimationLinkee Linkee;

    public bool Dispatch(string name)
    {
        return Linkee.TryAnimate(name);
    }
    
}