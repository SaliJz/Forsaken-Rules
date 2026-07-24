using UnityEngine;

public class DeathBoss : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _BossDeath;
    [SerializeField] private GameObject _Boss;

    [Header("Settings")]
    [SerializeField] private float _offsetX = 1;
    [SerializeField] private float _offsetY = 2;
    [SerializeField] private float _offsetZ = 0;

    private void OnDestroy()
    {
        if (_BossDeath == null || _Boss == null) return; 
        
        _BossDeath.transform.position = new Vector3(
            _Boss.transform.position.x + _offsetX,
            _Boss.transform.position.y + _offsetY,
            _Boss.transform.position.z + _offsetZ);
        _BossDeath.SetActive(true);
    }
}