using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AnimatorEventBridge
{
    private readonly Dictionary<(AnimationClip, int), Action<AnimationEvent>> frameEvents = new();

    public void RegisterFrame(AnimationClip clip, AnimationEvent animationEvent, Action<AnimationEvent> callback)
    {
        var key = (clip, GetFrame(clip, animationEvent));
        frameEvents.TryGetValue(key, out var frame);
        frameEvents[key] = frame + callback;
    }

    public void UnregisterFrame(AnimationClip clip, AnimationEvent animationEvent, Action<AnimationEvent> callback)
    {
        var key = (clip, GetFrame(clip, animationEvent));
        Action<AnimationEvent> frame = frameEvents[key] - callback;
        if (frame == null)
            frameEvents.Remove(key);
        else
            frameEvents[key] = frame;
    }

    public void ExecuteFrame(AnimationEvent animationEvent)
    {
        AnimationClip clip = animationEvent.animatorClipInfo.clip;
        var key = (clip, GetFrame(clip, animationEvent));
        if (frameEvents.TryGetValue(key, out var frame))
            frame(animationEvent);
    }

    private static int GetFrame(AnimationClip clip, AnimationEvent animationEvent)
        => Mathf.RoundToInt(animationEvent.time * clip.frameRate);
}
