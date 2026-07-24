using UnityEngine;
using System.Collections.Generic;

public class PlayerInputModifier : MonoBehaviour
{
    private struct DelayedInput
    {
        public Vector2 move;
        public float timestamp;
    }

    private Queue<DelayedInput> inputQueue = new Queue<DelayedInput>();
    private float effectTimer = 0f;
    private float currentLag = 0.15f;
    private float currentDrift = 0.5f;

    private Vector2 lastRawInput;

    void Update()
    {
        if (effectTimer > 0)
        {
            effectTimer -= Time.deltaTime;

            if (effectTimer <= 0)
            {
                inputQueue.Clear();
            }
        }
    }

    public void ApplyDebuff(float lag, float drift, float duration)
    {
        currentLag = lag / 1000f; 
        currentDrift = drift;
        effectTimer = duration;
    }

    public Vector2 ProcessInput(Vector2 rawInput)
    {
        if (effectTimer <= 0) return rawInput;

        if (rawInput == lastRawInput && inputQueue.Count == 0)
        {
            return ApplyDrift(rawInput);
        }

        lastRawInput = rawInput;

        inputQueue.Enqueue(new DelayedInput
        {
            move = rawInput,
            timestamp = Time.time + currentLag
        });

        if (inputQueue.Peek().timestamp <= Time.time)
        {
            return ApplyDrift(inputQueue.Dequeue().move);
        }

        return Vector2.zero;
    }

    private Vector2 ApplyDrift(Vector2 input)
    {
        if (input.magnitude < 0.1f) return input;

        float driftX = Mathf.Sin(Time.time * 3f) * currentDrift;
        input.x += driftX;

        return input.normalized * 1f;
    }
}