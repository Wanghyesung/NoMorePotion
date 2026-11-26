using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using Photon.Pun;
using Sirenix.OdinInspector;
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

    // 폭발 RPC 로 받는 공격력 배율의 상한입니다. (정상 값은 BuffContainer 상한인 1.7 배 이하)
    private const float MaxAttackMultiplier = 3f;

    public PotionData Data => potion.Data;
    public PotionInstance PotionInstance => potion.Instance;
    public bool HasExploded { get; private set; }
    private bool canExplodeOnContact;
    private System.Action<PotionInstance> onThrowLanded;
    private PhotonView photonView;

    private void Awake() => photonView = GetComponent<PhotonView>();

    [SerializeField] private LayerMask mask;
    public LayerMask CollisionMask => mask;
    [SerializeField] private GameObject impactPrefab;

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
        switch (Data.potionSize)
        {
            case PotionSize.Small:
                potionRenderer_S.material.color = Data.potionColor;
                potionMesh_S.SetActive(true);
                break;
            case PotionSize.Medium:
                potionRenderer_M.material.color = Data.potionColor;
                potionMesh_M.SetActive(true);
                break;
            case PotionSize.Large:
                potionRenderer_L.material.color = Data.potionColor;
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

    public void Throw(ThrowData data, System.Action<PotionInstance> onLanded = null)
    {
        onThrowLanded = onLanded;
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

        // 인스턴스가 없으면(데이터만 연결된 경우) 정상 포션으로 취급합니다.
        PotionInstance instance = PotionInstance ?? new PotionInstance(Data);

        // 던진(마신) 사람의 공격력 배율은 폭발 순간 값을 쓰고, 모든 기기가 같은 값으로 대미지를 계산하도록 함께 보냅니다.
        float attackMultiplier = GetThrowerAttackMultiplier();

        bool networked = photonView != null && photonView.ViewID != 0;
        if (networked)
        {
            // 함정 키워드는 폭발 순간에만 알립니다. (날아가는 동안에는 보내지 않아 함정 여부가 미리 드러나지 않음) 정상 포션은 0 입니다.
            int keywordId = instance.TrapKeyword != null ? instance.TrapKeyword.KeywordID : 0;
            photonView.RPC(nameof(RpcExplode), RpcTarget.Others, position, impact, instance.Data.potionID, keywordId, attackMultiplier);
        }

        PlayExplosion(position, impact, instance, attackMultiplier);
        // 폭발로 나무가 소진될 수 있으므로 효과 판정이 끝난 뒤 던질 때의 구독자에게 알린다.
        if (impact)
            onThrowLanded?.Invoke(instance);
        onThrowLanded = null;
        if (networked)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    [PunRPC]
    private void RpcExplode(Vector3 position, bool impact, int potionId, int keywordId, float attackMultiplier, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner || photonView.IsMine)
            return;

        // 삭제는 소유자가 뒤이어 보내는 PhotonNetwork.Destroy로 처리합니다.
        if (!PotionDataManager.Instance.TryGetPotionData(potionId, out PotionData data))
            return;

        // 키워드 ID 0 은 정상 포션입니다. 모르는 ID 는 경고하고 정상 포션으로 취급합니다.
        TrapPotionKeyword keyword = null;
        if (keywordId != 0 && !PotionDataManager.Instance.TryGetTrapKeyword(keywordId, out keyword))
            Debug.LogWarning($"[PotionMover] : 알 수 없는 함정 키워드 번호({keywordId})를 받아 정상 포션으로 폭발시킵니다.", this);

        // 정상 값은 BuffContainer 상한(최대 1.7배) 안이지만, 깨진 값이 와도 대미지가 터무니없어지지 않게 제한합니다.
        attackMultiplier = Mathf.Clamp(attackMultiplier, 0f, MaxAttackMultiplier);

        PlayExplosion(position, impact, new PotionInstance(data, keyword), attackMultiplier);
    }

    // 던진(마신) 사람의 공격력 배율입니다. 폭발은 이 포션의 주인 기기에서만 시작하므로 이 기기의 내 플레이어 값이 곧 던진 사람의 값입니다.
    private static float GetThrowerAttackMultiplier()
    {
        SurviveStatusManager status = SurviveStatusManager.Instance;
        BuffContainer container = status != null ? status.PlayerBuffContainer : null;

        return container != null ? container.GetStatMultiplier(StatType.AttackPower) : 1f;
    }

    // 던진 기기도, RPC 를 받은 기기도 같은 경로입니다. 어떤 대상을 처리할지는 PotionDetector 가 이 기기의 역할(방장인지)로 정합니다.
    private void PlayExplosion(Vector3 position, bool impact, PotionInstance instance, float attackMultiplier)
    {
        if (HasExploded)
            return;

        HasExploded = true;
        canExplodeOnContact = false;
        movement.enabled = false;
        if (impact)
            PlayImpact(position);

        PotionData data = instance.Data;
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
                // 모든 기기가 같은 정보(포션, 키워드, 공격력 배율)로 초기화합니다.
                detector.Initialize(instance, attackMultiplier);
            }

        }

    }

    // hitPoint: 포션 콜라이더로 검사한 월드 충돌 위치
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
            // 충격 파티클 반납도 모든 기기가 각자 돌려야 하므로 TimeManager.Schedule 을 쓰지 않는다. (Schedule 은 방장만 실행)
            ReleaseParticleAsync(particleObj, 1.0f).Forget();
        }

    }

    // 포션(this)은 바로 Destroy되므로 이 오브젝트의 토큰을 쓰지 않고, 토큰은 파티클 것을 사용한다. (반납·파괴 시 취소)
    private static async UniTaskVoid ReleaseParticleAsync(GameObject particleObj, float delaySeconds)
    {
        bool cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(delaySeconds),
                cancellationToken: PoolManager.GetReleaseToken(particleObj))
            .SuppressCancellationThrow();
        if (cancelled)
            return;

        if (PoolManager.Instance == null) // 씬 종료 등으로 PoolManager가 먼저 사라진 경우
            return;

        PoolManager.Instance.Release(particleObj);
    }
}
