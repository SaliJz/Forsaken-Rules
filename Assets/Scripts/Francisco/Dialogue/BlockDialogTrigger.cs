using UnityEngine;
using UnityEngine.Events;

public class BlockDialogTrigger : MonoBehaviour
{
    [Header("Dependencies")]
    private PlayerBlockSystem blockSystem;

    [Header("Dialog Configuration")]
    public DialogLine[] blockStartDialog;
    public UnityEvent OnBlockDialogFinish;

    [Header("Event on Dialog End")]
    private bool hasTriggeredBlockDialog = false;

    [SerializeField] private UnityEvent onComplete;

    private void Awake()
    {
        blockSystem = FindAnyObjectByType<PlayerBlockSystem>();
        if (blockSystem == null)
        {
            Debug.LogError("PlayerBlockSystem no se encuentra en el mismo GameObject que BlockDialogTrigger.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (blockSystem != null)
        {
            blockSystem.OnBlockStart += TriggerDialogOnBlock;
        }
    }

    private void OnDisable()
    {
        if (blockSystem != null)
        {
            blockSystem.OnBlockStart -= TriggerDialogOnBlock;
        }
    }

    public void TriggerDialogOnBlock()
    {
        if (hasTriggeredBlockDialog) return;

        onComplete?.Invoke();

        if (DialogManager.Instance != null && blockStartDialog != null && blockStartDialog.Length > 0)
        {
            DialogManager.Instance.StartDialog(blockStartDialog, OnBlockDialogFinish);
            hasTriggeredBlockDialog = true;
            Debug.Log("Diálogo iniciado tras detectar el inicio del bloqueo.");
        }
        else
        {
            if (DialogManager.Instance == null)
            {
                Debug.LogWarning("No se pudo iniciar el diálogo: DialogManager.Instance es nulo.");
            }
            else
            {
                Debug.LogWarning("No se pudo iniciar el diálogo: 'blockStartDialog' está vacío o nulo.");
            }
        }
    }
}