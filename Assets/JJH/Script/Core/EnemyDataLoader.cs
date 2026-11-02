using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static JJH_Constants;

public class EnemyDataLoader : MonoBehaviour
{
    public EnemyDataLoader Instance { get; private set; }

    private Dictionary<int, MonsterData> NormalMonsterData;
    public List<MonsterData> NormalMonsterDataList;
    private Dictionary<int, MonsterData> BossMonsterData;
    public List<MonsterData> BossMonsterDataList;
    private Dictionary<int, MonsterTextData> MonsterLocalizationData;
    public List<MonsterTextData> MonsterLocalizationDataList;

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);

    }

    public void Start()
    {
        NormalMonsterDataList = new();
        NormalMonsterData = new();
        BossMonsterDataList = new();
        BossMonsterData = new();
        MonsterLocalizationDataList = new();
        MonsterLocalizationData = new();
    }

    public void ResourceLoad()
    {
        string normalData = "", bossData = "", localizationData = "";

        // 이후 옵션에서 받아 설정할 것 
        string nowCont = COUNTRY_KR;

        try
        {
            normalData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{NORMAL_TEXT}").text;
            bossData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{BOSS_TEXT}").text;
            localizationData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{TEXT}_{nowCont}").text;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataLoad] 몬스터 데이터 로드 실패 : {e.Message}");
        }

        try
        {
            // 리스트 변환
            NormalMonsterDataList = JsonConvert.DeserializeObject<List<MonsterData>>(normalData);
            BossMonsterDataList = JsonConvert.DeserializeObject<List<MonsterData>>(bossData);
            MonsterLocalizationDataList = JsonConvert.DeserializeObject<List<MonsterTextData>>(localizationData);

            // 딕셔너리 가공
            NormalMonsterData = NormalMonsterDataList?.ToDictionary(x => x.Id) ?? new Dictionary<int, MonsterData>();
            BossMonsterData = BossMonsterDataList?.ToDictionary(x => x.Id) ?? new Dictionary<int, MonsterData>();
            MonsterLocalizationData = MonsterLocalizationDataList?.ToDictionary(x => x.Id) ?? new Dictionary<int, MonsterTextData>();
        }
        catch (JsonException jsonEx)
        {
            // JSON 문법(쉼표 누락, 중괄호 미닫힘 등) 오류 발생 시
            Debug.LogError($"[JSON Parsing] JSON 데이터 파싱 중 문법 오류 발생: {jsonEx.Message}");
        }
        catch (Exception e)
        {
            // 이외 런타임 에러
            Debug.LogError($"[DataProcessing] 데이터 변환 및 딕셔너리 구성 중 오류 발생: {e.Message}");
        }

    }

    public void ReloadLocalizationData()
    {
        string localizationData = "";

        // 이후 옵션에서 받아 설정할 것 
        string nowCont = COUNTRY_KR;

        try
        {
            localizationData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{TEXT}_{nowCont}").text;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataLoad] 몬스터 데이터 로드 실패 : {e.Message}");
        }

        try
        {
            // 리스트 변환
            MonsterLocalizationDataList = JsonConvert.DeserializeObject<List<MonsterTextData>>(localizationData);

            // 딕셔너리 가공
            MonsterLocalizationData = MonsterLocalizationDataList?.ToDictionary(x => x.Id) ?? new Dictionary<int, MonsterTextData>();
        }
        catch (JsonException jsonEx)
        {
            // JSON 문법(쉼표 누락, 중괄호 미닫힘 등) 오류 발생 시
            Debug.LogError($"[JSON Parsing] JSON 데이터 파싱 중 문법 오류 발생: {jsonEx.Message}");
        }
        catch (Exception e)
        {
            // 이외 런타임 에러
            Debug.LogError($"[DataProcessing] 데이터 변환 및 딕셔너리 구성 중 오류 발생: {e.Message}");
        }
    }

    public MonsterData LoadNormalData(int id)
    {
        if (NormalMonsterData.TryGetValue(id, out MonsterData data))
            return data;
        else
            return null;
    }

    public MonsterData LoadBossData(int id)
    {
        if (BossMonsterData.TryGetValue(id, out MonsterData data))
            return data;
        else
            return null;
    }

    public MonsterTextData LoadTextData(int id)
    {
        if (MonsterLocalizationData.TryGetValue(id, out MonsterTextData data))
            return data;
        else
            return null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
