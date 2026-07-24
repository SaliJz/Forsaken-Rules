using UnityEngine;
using UnityEngine.Playables;

public class TimelineDialogTrigger : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayableDirector director;

    private void OnEnable()
    {
        if (director != null && director.state == PlayState.Playing)
        {
            director.Pause();
        }
    }

    public void ReanudarTimeline()
    {
        if (director != null)
        {
            director.Play();
        }
    }
}