using System;
using UnityEngine;

[Serializable]
public struct PlayerNode
{
    public float timeTrigger;
    public Transform nodeTransform;
    public float speed;
    public bool useYAxis;
}

[Serializable]
public struct PlatformNode
{
    public float timeTrigger;
    public Vector3 movementOffset;
    public float speed;
}

[Serializable]
public struct FadeEvent
{
    public float timeTrigger;
}

[Serializable]
public struct AnimationEvent
{
    public float timeTrigger;
    public Animator animator;
    public string paramName;
    public bool value;
}