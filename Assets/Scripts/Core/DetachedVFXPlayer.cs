using System.Collections;
using UnityEngine;

public class DetachedVFXPlayer : MonoBehaviour
{
    Transform _home;
    Vector3 _homeLP; Quaternion _homeLR; Vector3 _homeLS;
    ParticleSystem _ps;

    void Awake() { _ps = GetComponent<ParticleSystem>(); }

    public void PlayDetached(
        Transform home, Vector3 homeLocalPos, Quaternion homeLocalRot, Vector3 homeLocalScale,
        Vector3 worldPos, Quaternion worldRot, Vector3 playScale, float lifeSeconds)
    {
        _home = home; _homeLP = homeLocalPos; _homeLR = homeLocalRot; _homeLS = homeLocalScale;

        // Tách khỏi enemy để không bị tắt theo
        transform.SetParent(null, true);
        transform.position = worldPos;
        transform.rotation = worldRot;
        transform.localScale = playScale;

        gameObject.SetActive(true);
        if (_ps) { _ps.Clear(true); _ps.Play(true); }

        StartCoroutine(ReturnAfter(lifeSeconds));
    }

    IEnumerator ReturnAfter(float t)
    {
        yield return new WaitForSeconds(t);

        if (_ps) _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);

        // Trả về "nhà" (kể cả khi home đang inactive)
        if (_home)
        {
            transform.SetParent(_home, false);
            transform.localPosition = _homeLP;
            transform.localRotation = _homeLR;
            transform.localScale = _homeLS;
        }
    }
}