using System;
using UnityEngine;

public abstract class BaseAnimCtrl<TState> : MonoBehaviour where TState : Enum
{
    protected SpriteAnimator SA;
    protected JsonAnimProvider provider;

    // [SerializeField] protected GameObject libraryObj; // En caso de no asignar un provider directamente, se buscara uno en este objeto
    [SerializeField] protected JsonAnimLibrarySO librarySO; // Si se asigna, se usara este ScriptableObject como provider

    [Header("Current Status")]
    public TState currentState;
    public string currentDirection = "down";
    public AnimPriority currentPriority = AnimPriority.none;
    protected string lastDirection = "down";
    protected bool isForcedAnim;

    public enum AnimPriority
    {
        none = 0,
        locomotion = 5, //Idle, Wallk
        damage = 10, //Interact, PickUp
        action = 20, //Melee, Distance
        attack = 25, //Hit Stun
        dash = 30, //Dash
        bind = 40 //Cutscenes, frozen
    }

    void Awake()
    {
        SA = GetComponent<SpriteAnimator>();

        if(librarySO != null) provider = librarySO;
        else provider = GetComponent<JsonAnimProvider>();

        if(SA != null && provider != null)
        {
            // Debug.LogError($"[BaseAnimCtrl] {name} no tiene ningun provider asignado");
            SA.SetProvider(provider);
        }
    }

    protected virtual void Start()
    {
        SA.onAnimFinished += InternalOnAnimFinished;
        SA.onAnimEvent += OnAnimationEvent;
    }

    public virtual void PlayState(TState state, AnimPriority priority, bool reset = true)
    {
        if(priority < currentPriority) return;

        string baseID = state.ToString().ToLower();
        string finalID = ResolveFullID(baseID, lastDirection);

        if(!provider.AnimExist(finalID)) finalID = baseID;

        // if(state.Equals(currentState) && lastDirection == currentDirection && !reset) return;

        currentState = state;
        currentPriority = priority;
        // currentDirection = lastDirection;
        isForcedAnim = (priority > AnimPriority.locomotion);

        // string ResolvedID = ResolveFullID(state.ToString(), lastDirection);
        SA.Play(finalID, lastDirection, reset);
    }

    protected abstract string ResolveFullID(string baseID, string direction);

    void InternalOnAnimFinished()
    {
        if (isForcedAnim)
        {
            isForcedAnim = false;
            currentPriority = AnimPriority.none;
        }

        OnFinishedAnimation();
    }

    protected abstract void OnFinishedAnimation();
    protected abstract void OnAnimationEvent(string ev);

    public void UpdateDirection(float h, float v)
    {
        if(h == 0 && v == 0) return;

        if (h == 0 && v > 0) lastDirection = "up";
        else if (h == 0 && v < 0) lastDirection = "down";
        else if (h > 0 && v == 0) lastDirection = "right";
        else if (h < 0 && v == 0) lastDirection = "left";
        else if (h > 0 && v > 0) lastDirection = "upright";
        else if (h < 0 && v > 0) lastDirection = "upleft";
        else if (h > 0 && v < 0) lastDirection = "downright";
        else if (h < 0 && v < 0) lastDirection = "downleft"; ;

        currentDirection = lastDirection;
    }
}
