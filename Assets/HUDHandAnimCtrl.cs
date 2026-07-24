using System.Collections;
using UnityEngine;

public class HUDHandAnimCtrl : MonoBehaviour
{
    [SerializeField] private RectTransform[] positions;
    [SerializeField] private RectTransform hand;
    [SerializeField] private Animator anim;
    [SerializeField] private RectTransform HUDtoShake;
    public RectTransform getHUDToShake => HUDtoShake;

    [Header("Anim Timming")]
    [SerializeField] private float timeToSmash = 0.5f;
    [SerializeField] private AnimationCurve SmashEase;
    [SerializeField] private float timeToFall = 1f;
    [SerializeField] private AnimationCurve FallEase;

    [Header("Shake")]
    [SerializeField] float shakeDuration = 0.3f;
    [SerializeField] float shakeIntensity = 10f;

    string baseAnimID = "IdleHand";
    string triggerName = "Close";

    public System.Action onSmashImpact;

    Coroutine activeSecuence;

    // void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.Y))
    //     {
    //         PlaySmashSecuence();
    //     }
    // }

    public void PlaySmashSecuence()
    {
        StopSmashSecuence();
        activeSecuence = StartCoroutine(SmashSecuence());
    }

    public void StopSmashSecuence()
    {
        StopAllCoroutines();
        hand.position = positions[0].position;
    }


    public IEnumerator SmashSecuence()
    {
        anim.Play(baseAnimID);
        anim.SetTrigger(triggerName);
        yield return StartCoroutine(LerpHandPosition(positions[0], positions[1], timeToSmash, SmashEase));

        onSmashImpact?.Invoke();
        StartCoroutine(ShakeHUD());

        yield return new WaitForSeconds(0.25f);
        
        yield return StartCoroutine(LerpHandPosition(positions[1], positions[2], timeToFall, FallEase));
        activeSecuence = null;
    }

    IEnumerator LerpHandPosition(RectTransform from, RectTransform to, float duration, AnimationCurve curve)
    {
        Vector2 startPos = from.anchoredPosition;
        Vector2 endPos = to.anchoredPosition;
        float elapseTime = 0f;

        while (elapseTime < duration)
        {
            elapseTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapseTime / duration);
            float easedT = curve.Evaluate(t);

            hand.anchoredPosition = Vector2.Lerp(startPos, endPos, easedT);
            yield return null;
        }

        hand. anchoredPosition = endPos;
    }

    IEnumerator ShakeHUD()
    {
        if(HUDtoShake == null) yield break;

        Vector2 originalPos = HUDtoShake.localPosition;
        float elapsedTime = 0f;

        while (elapsedTime < shakeDuration)
        {
            elapsedTime += Time.deltaTime;
            float randomX = Random.Range(-shakeIntensity, shakeIntensity);
            float randomY = Random.Range(-shakeIntensity, shakeIntensity);

            HUDtoShake.localPosition = originalPos + new Vector2 (randomX, randomY);
            yield return null;
        }

        HUDtoShake.localPosition = originalPos;
    }



}
