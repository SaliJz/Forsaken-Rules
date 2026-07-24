using System.Collections;
using UnityEngine;
using System;

public class GachaEyeDrop : MonoBehaviour
{
    [SerializeField] Transform launchPos;
    [SerializeField] Transform targetPos;
    float throwDuration = 0.4f;
    float arcHeight = 0.5f;
    bool hasLanded = false;

    public Action OnEyeCollected;

    public void LaunchEye()
    {
        gameObject.SetActive(true);
        hasLanded = false;
        StartCoroutine(ThrowRoutine());
    }

    IEnumerator ThrowRoutine()
    {
        Vector3 start = launchPos.position;
        Vector3 end = targetPos.position;

        float elapse = 0f;

        while (elapse < throwDuration)
        {
            float t = elapse / throwDuration;

            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y += arcHeight * 20f * t * (1f - t);

            transform.position = pos;
            elapse += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        hasLanded = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && hasLanded)
        {
            //Effect
            PlayerShaderCtrl shader = other.GetComponent<PlayerShaderCtrl>();

            if (shader == null)
            {
                shader = other.GetComponentInChildren<PlayerShaderCtrl>();
            }
            if (shader == null)
            {
                shader = other.GetComponentInParent<PlayerShaderCtrl>();
            }

            shader?.ShineTrigger();

            OnEyeCollected?.Invoke();

            //REFERENCIA O LLAMADA EXTERNA PARA DAR EL BOOST, QUIZA LUEGO HACER QUE EL OJO VAYA DIRECTO AL JUGADOR
            gameObject?.SetActive(false);
        }
    }
}