using System.Collections;
using UnityEngine;

public class GachaAnimCtrl : MonoBehaviour
{
    [Header("REFERENCES")]
    [SerializeField] Animator handAnimator;
    [SerializeField] Animator machineAnimator;
    [SerializeField] Transform shakeTarget;
    [SerializeField] Transform LaunchPos;
    // [SerializeField] Transform Eye;
    [SerializeField] GachaEyeDrop eyeScript;

    [Header("SHAKE")]
    public float shakeDuration = 1f;
    [SerializeField] float shakeStrength = 0.2f;
    [SerializeField] float shakeFrecuency = 25f;
    
    public bool isAnimating = false;
    public bool IsAnimating { get { return isAnimating; } }
    public GachaEyeDrop EyeScript => eyeScript;

    struct HandAnims
    {
        public string idle;
        public string look;
        public string spit;
    }
    struct MachineAnims
    {
        public string idle;
        public string active;
        public string givePrice;
    }

    HandAnims handAnims;
    MachineAnims machineAnims;

    void GetAnims()
    {
        handAnims.idle = "idle";
        handAnims.look = "LookMachine";
        handAnims.spit = "SpitPrice";
        machineAnims.idle = "idle?";
        machineAnims.active = "Active";
        machineAnims.givePrice = "GivePrice";
    }

    void Awake()
    {
        GetAnims();
    }

    void Update()
    {
        //if(Input.GetKeyDown(KeyCode.P)) {ActivateGacha();}
    }

    public void ActivateGacha()
    {
        StartCoroutine(GachaSecuence());
    }

    IEnumerator GachaSecuence()
    {
        isAnimating = true;
        
        handAnimator.Play(handAnims.look, 0, 0);
        machineAnimator.Play(machineAnims.active, 0, 0);

        yield return StartCoroutine(Shake(shakeDuration));

        //Final
        machineAnimator.Play(machineAnims.givePrice, 0, 0f);
        yield return new WaitForSeconds(0.35f);
        handAnimator.Play(handAnims.spit, 0, 0f);
        //SpawnEye
        // yield return new WaitForSeconds(0.8f);
        // LaunchEyeEvent();

        // isAnimating = false;
    }

    IEnumerator Shake(float duration)
    {
        Vector3 originalPos = shakeTarget.localPosition;
        float time = 0f;

        while (time < duration)
        {
            float offsetX = Mathf.Sin(Time.time * shakeFrecuency) * shakeStrength;
            float offsetY = Mathf.Cos(Time.time * shakeFrecuency) * shakeStrength;

            shakeTarget.localPosition = originalPos + new Vector3 (offsetX, offsetY, 0f);

            time += Time.deltaTime;
            yield return null;
        }

        shakeTarget.localPosition = originalPos;
    }

    public void LaunchEyeEvent()
    {
        eyeScript.LaunchEye();
    }
}