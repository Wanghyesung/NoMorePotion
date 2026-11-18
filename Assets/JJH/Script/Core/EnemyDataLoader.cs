using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static JJH_Constants;

using HIn = UnityEngine.HideInInspector;

public class EnemyDataLoader : MonoBehaviour
{
    public static EnemyDataLoader Instance { get; private set; }

    private Dictionary<int, MonsterData> NormalMonsterData;

    public List<MonsterData> NormalMonsterDataList;
    private Dictionary<int, BossData> BossMonsterData;
    public List<BossData> BossMonsterDataList;
    private Dictionary<int, MonsterTextData> MonsterLocalizationData;
    public List<MonsterTextData> MonsterLocalizationDataList;
    private Dictionary<int, List<BossSkill>> SkillData;
    public List<BossSkill> SkillDataList;

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);

        NormalMonsterDataList = new();
        NormalMonsterData = new();
        BossMonsterDataList = new();
        BossMonsterData = new();
        MonsterLocalizationDataList = new();
        MonsterLocalizationData = new();
        SkillDataList = new();
        SkillData = new();

        ResourceLoad();
    }

    public void Start()
    {
        
    }

    public void ResourceLoad()
    {
        string normalData = "", bossData = "", localizationData = "", skillData = "";

        // 이후 옵션에서 받아 설정할 것 
        string nowCont = COUNTRY_KR;

        try
        {
            normalData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{NORMAL_TEXT}").text;
            bossData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{BOSS_TEXT}").text;
            localizationData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{TEXT}_{nowCont}").text;
            skillData = Resources.Load<TextAsset>($"{ASSET_DATA_PATH}{MONSTER_DATA}_{SKILL_TEXT}").text;

        }
        catch (Exception e)
        {
            Debug.LogError($"[DataLoad] 몬스터 데이터 로드 실패 : {e.Message}");
        }

        try
        {
            // 리스트 변환
            NormalMonsterDataList = JsonConvert.DeserializeObject<List<MonsterData>>(normalData);
            BossMonsterDataList = JsonConvert.DeserializeObject<List<BossData>>(bossData);
            MonsterLocalizationDataList = JsonConvert.DeserializeObject<List<MonsterTextData>>(localizationData);
            SkillDataList = JsonConvert.DeserializeObject<List<BossSkill>>(skillData);

            // 딕셔너리 가공
            NormalMonsterData = NormalMonsterDataList?.ToDictionary(x => x.Id) ?? new Dictionary<int, MonsterData>();
            BossMonsterData = BossMonsterDataList?.ToDictionary(x => x.Id) ?? new Dictionary<int, BossData>();
            MonsterLocalizationData = MonsterLocalizationDataList?.ToDictionary(x => x.Id) ?? new Dictionary<int, MonsterTextData>();
            SkillData = BuildSkillTable(SkillDataList);
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
    /// <summary>
    /// 몬스터 ID별 스킬 묶음 (SkillNum 순 정렬 + 번호 범위·중복 검사)
    /// </summary>
    private static Dictionary<int, List<BossSkill>> BuildSkillTable(List<BossSkill> list)
    {
        var table = new Dictionary<int, List<BossSkill>>();
        if (list == null) return table;

        foreach (var group in list.GroupBy(s => s.UseMonsterId))
        {
            List<BossSkill> skills = group.OrderBy(s => s.SkillNum).ToList();
            var used = new HashSet<int>();

            foreach (BossSkill s in skills)
            {
                if (s.SkillNum < 1 || s.SkillNum > MAX_SKILL_SLOT)
                    Debug.LogError($"[DataLoad] 스킬 번호 범위 밖(1~{MAX_SKILL_SLOT}) : 몬스터 {s.UseMonsterId} / {s.SkillNum}");
                if (!used.Add(s.SkillNum))
                    Debug.LogError($"[DataLoad] 스킬 번호 중복 : 몬스터 {s.UseMonsterId} / {s.SkillNum}");
            }

            table[group.Key] = skills;
        }
        return table;
    }

    /// <summary>
    /// 몬스터의 스킬 목록 (없으면 빈 리스트)
    /// </summary>
    public List<BossSkill> LoadSkills(int monsterId)
    {
        if (SkillData.TryGetValue(monsterId, out List<BossSkill> data))
            return data;
        else
            return new();
    }
        

    public MonsterData LoadNormalData(int id)
    {
        if (NormalMonsterData.TryGetValue(id, out MonsterData data))
            return data;
        else
            return null;
    }

    public BossData LoadBossData(int id)
    {
        if (BossMonsterData.TryGetValue(id, out BossData data))
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
