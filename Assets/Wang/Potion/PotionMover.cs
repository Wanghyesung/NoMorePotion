using Enzeun.Runtime;
using Enzeun.SO;
using Photon.Pun;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;

/*///////////////////////////////////////////
                       TestPotion
기능 : 인벤토리와 손에서 사용하는 포션 아이템 컴포넌트
 *///////////////////////////////////////////

[RequireComponent(typeof(ParabolicMovement))]
public class PotionMover : MonoBehaviour
{
    [SerializeField]
    private ParabolicMovement movement;

    [SerializeField]
    private Potion potion;

    public PotionData Data => potion.Data;
    public PotionInstance PotionInstance => potion.Instance;
    public bool HasExploded { get; private set; }
    private bool canExplodeOnContact;
    private PhotonView photonView;

    private void Awake() => photonView = GetComponent<PhotonView>();

    [SerializeField] private LayerMask mask;
    [SerializeField] private GameObject impactPrefab;

    //[SerializeField, Min(0f)] private float impactLifetime = 5f;

    [SerializeField, BoxGroup("포션 연결"), LabelText("포션 메시 (소)")]
    private GameObject potionMesh_S;
    [SerializeField, BoxGroup("포션 연결"), LabelText("포션 메시 (중)")]
    private GameObject potionMesh_M;
    [SerializeField, BoxGroup("포션 연결"), LabelText("포션 메시 (대)")]
    private GameObject potionMesh_L;
    [SerializeField, BoxGroup("포션 연결"), LabelText("액체 (소)")]
    private Renderer potionRenderer_S;
    [SerializeField, BoxGroup("포션 연결"), LabelText("액체 (중)")]
    private Renderer potionRenderer_M;
    [SerializeField, BoxGroup("포션 연결"), LabelText("액체 (대)")]
    private Renderer potionRenderer_L;


    public void InitializePotionMover()
    {
        if (potion == null)
        {
            Debug.LogError("[TestPotion] : Potion 정보가 들어오지 않았습니다. Critical Error 입니다.");
            Destroy(gameObject);
            return;
        }

        if (potion.Data == null)
        {
            Debug.LogError("[TestPotion] : Potion.Data 정보가 들어오지 않았습니다. Critical Error 입니다.");
            Destroy(gameObject);
            return;
        }

#if UNITY_EDITOR
        if (potionRenderer_S == null || impactPrefab == null)
        {
            Debug.LogWarning("포션 렌더러 , 파티클이 연결되지 않았습니다. 포션 색상 변경이 적용되지 않습니다.", this);
        }
#endif

        if (movement == null)
        {
            movement = GetComponent<ParabolicMovement>();
        }

        HasExploded = false;
        canExplodeOnContact = false;
        movement.OnHitAction += HandleImpact;
        InitMeshAndColor();
    }

    public void InitMeshAndColor()
    {
        var p = Data.potionSize;
        switch (p)
        {
            case PotionSize.Small:
                potionRenderer_S.material.color = Data != null ? Data.potionColor : Color.white;
                potionMesh_S.SetActive(true);
                break;
            case PotionSize.Medium:
                potionRenderer_M.material.color = Data != null ? Data.potionColor : Color.white;
                potionMesh_M.SetActive(true);
                break;
            case PotionSize.Large:
                potionRenderer_L.material.color = Data != null ? Data.potionColor : Color.white;
                potionMesh_L.SetActive(true);
                break;
        }

    }

    private void OnDisable()
    {
        movement.OnHitAction -= HandleImpact;
    }

    // 손에 보관하는 동안에는 비행과 충돌 검사를 실행하지 않는다.
    public void Hold(Transform socket)
    {
        canExplodeOnContact = false;
        movement.AttachTo(socket);
    }

    public void Throw(ThrowData data)
    {
        movement.enabled = true;
        movement.Launch(data, mask);
        canExplodeOnContact = true;
    }

    public void ThrowVisual(ThrowData data, float elapsedSeconds)
    {
        canExplodeOnContact = false;
        movement.enabled = true;
        movement.Launch(data, mask, elapsedSeconds, false);
    }

    public void Drink(Vector3 position)
    {
        canExplodeOnContact = false;
        ExplodeAt(position);
    }

    public void ExplodeAt(Vector3 position)
    {
        FinishExplosion(position, false);
    }

    private void FinishExplosion(Vector3 position, bool impact)
    {
        if (HasExploded || (photonView != null && photonView.ViewID != 0 && !photonView.IsMine))
            return;

        bool networked = photonView != null && photonView.ViewID != 0;
        if (networked)
            photonView.RPC(nameof(RpcExplode), RpcTarget.Others, position, impact, Data.potionID,
                PotionInstance != null && PotionInstance.TrapKeyword != null ? PotionInstance.TrapKeyword.KeywordID : 0);

        PlayExplosion(position, impact, true, Data, null);
        if (networked)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    [PunRPC]
    private void RpcExplode(Vector3 position, bool impact, int potionId, int keywordId, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner || photonView.IsMine)
            return;

        // 삭제는 소유자가 뒤이어 보내는 PhotonNetwork.Destroy로 처리합니다.
        if (!PotionDataManager.Instance.TryGetPotionData(potionId, out PotionData data))
            return;
        TrapPotionKeyword keyword = null;
        if (keywordId != 0 && !PotionDataManager.Instance.TryGetTrapKeyword(keywordId, out keyword))
            return;

        PlayExplosion(position, impact, false, data, keyword);
    }

    private void PlayExplosion(Vector3 position, bool impact, bool applyEffects, PotionData data, TrapPotionKeyword keyword)
    {
        if (HasExploded)
            return;

        HasExploded = true;
        canExplodeOnContact = false;
        movement.enabled = false;
        if (impact)
            PlayImpact(position);

        if (data.explosionPrefab != null)
        {
            var explosion = PoolManager.Instance.Get(data.explosionPrefab, position, Quaternion.identity);
            if (explosion == null)
            {
                Debug.LogError("[Potionover] : 폭발 프리팹이 없습니다");
                return;
            }
            if (explosion.TryGetComponent(out PotionDetector detector))
            {
                if (applyEffects)
                    detector.Initialize(PotionInstance);
                else
                    detector.InitializeVisual(data);
            }

        }

    }

    // hitPoint: SphereCast로 얻은 월드 충돌 위치
    private void HandleImpact(Vector3 hitPoint)
    {
        if (canExplodeOnContact == false || HasExploded == true)
            return;

        FinishExplosion(hitPoint, true);
    }

    private void PlayImpact(Vector3 hitPoint)
    {
        if (impactPrefab != null)
        {
            GameObject particleObj =
                PoolManager.Instance.Get(impactPrefab, hitPoint, Quaternion.identity);

            var particle = particleObj.GetComponent<ParticleSystem>();

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Color color = Data != null ? Data.potionColor : Color.white;
            var main = particle.main;
            main.startColor = color;

            particle.Play(true);
            // 토큰은 파티클 것을 사용: 포션(this)은 바로 Destroy되므로 destroyCancellationToken을 쓰면 반납 예약도 같이 취소됨
            TimeManager.Instance.Schedule(1.0f, () =>
            {
                if (PoolManager.Instance == null) // 씬 종료 등으로 PoolManager가 먼저 사라진 경우
                    return;

                PoolManager.Instance.Release(particleObj);
            }, PoolManager.GetReleaseToken(particleObj));
        }

    }
}
