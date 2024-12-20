using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static DataManager;
using static Enums;

public class GameManager
{
    public Player player;
    public List<Monster> MonsterList = new();

    #region 스탯 계산
    private Dictionary<int, Item> OwnedItems = new Dictionary<int, Item>();
    private Dictionary<ItemType, Item> EquippedItems = new Dictionary<ItemType, Item>();

    private Stats _baseStats = new Stats();         // 플레이어 기본 스탯
    public Stats _upgradeStats = new Stats();      // 스탯 레벨업 스탯
    private Stats _equipmentStats = new Stats();    // 장비 스탯

    public event Action<Stats> OnStatsChanged;  // 스탯 변경 이벤트 추가
    public int loadedStage { get; set; }
    public int loadedGold { get; set; }


    //전체 스탯 합산
    public Stats UpdateTotalStat()
    {
        Stats total = new Stats();
        total.CopyStats(player._baseStats);  // 기본 스탯
        total.AddStats(_equipmentStats);     // 장비 스탯
        total.AddStats(_upgradeStats);       // 업그레이드 스탯
        return total;
    }

    public void UpdatePlayerStats()
    {
        if (player == null) return;
        float currentHpRatio = (float)player._currentStats.Hp / player._currentStats.MaxHp;

        //// 현재 스탯을 기본 스탯으로 초기화
        player._currentStats.ClearStat();
        player._currentStats.CopyStats(player._baseStats); //기본스탯만 적용
        Debug.Log($"기본 스탯 적용 - ATK: {player._currentStats.Attack}, DEF: {player._currentStats.Defense}");

        // 업그레이드 스탯 적용
        if (_upgradeStats != null)
        {
            Debug.Log($"업그레이드 스탯 - ATK: {_upgradeStats.Attack}, DEF: {_upgradeStats.Defense}");
            player._currentStats.AddStats(_upgradeStats);
            Debug.Log($"업그레이드 적용 후 - ATK: {player._currentStats.Attack}, DEF: {player._currentStats.Defense}");
        }

        // 장비 스탯 적용
        if (_equipmentStats != null)
        {
            Debug.Log($"장비 스탯 - ATK: {_equipmentStats.Attack}, DEF: {_equipmentStats.Defense}");
            player._currentStats.AddStats(_equipmentStats);
            Debug.Log($"장비 적용 후 - ATK: {player._currentStats.Attack}, DEF: {player._currentStats.Defense}");
        }

        // 버프 스탯 재적용
        if (player is Player playerObj)
        {
            foreach (var buff in playerObj.GetActiveBuffs())
            {
                player._currentStats.AddStats(buff.BuffStats);
            }
        }
        player._currentStats.Hp = Mathf.RoundToInt(player._currentStats.MaxHp * currentHpRatio);
        Debug.Log($"최종 스탯 - ATK: {player._currentStats.Attack}, DEF: {player._currentStats.Defense}");
    }

    public void CalcEquipItem()
    {
        // 장비 스탯 초기화
        _equipmentStats.ClearStat();

        // 모든 장착 장비의 스탯 합산
        foreach (Item item in EquippedItems.Values)
        {
            if (item != null)
            {
                _equipmentStats.AddStatByType(StatType.Atk, item.CurrentAtk);
                _equipmentStats.AddStatByType(StatType.Def, item.CurrentDef);
                Debug.Log($"장비 스탯 계산: {item.Type}, ATK +{item.CurrentAtk}, DEF +{item.CurrentDef}");
            }
        }
        //플레이어 스탯 업데이트
        if (player != null)
        {
            UpdatePlayerStats();
        }
    }

    #endregion

    #region Equipment
    public Item GetOwnedItem(int dataId)
    {
        if (OwnedItems.TryGetValue(dataId, out Item item))
        {
            return item;
        }
        return null;
    }
    public bool HasItem(int dataId)
    {
        return OwnedItems.ContainsKey(dataId);
    }   
    public bool IsEquipped(Item item)
    {
        if (EquippedItems.TryGetValue(item.Type, out Item equippedItem))
        {
            return equippedItem.DataId == item.DataId;
        }
        return false;
    }
    public bool TryBuyItem(Item item)
    {
        //이미 가지고 있으면 재구매 불가
        if (HasItem(item.DataId)) return false;

        if (Managers.Instance.Currency.GetCurrentGold() >= item.ItemPrice)
        {
            Managers.Instance.Currency.RemoveGold(item.ItemPrice);
            OwnedItems.Add(item.DataId, item);
            return true;
        }
        return false;
    }
    public bool TryUnequipItem(ItemType itemType)
    {
        if (!EquippedItems.TryGetValue(itemType, out Item unequippedItem))
            return false;

        EquippedItems.Remove(itemType);
        CalcEquipItem();
        UpdatePlayerStats();

        return true;
    }
    public bool TryEquipItem(Item item)
    {
        if (!HasItem(item.DataId)) return false;

        Item ownedItem = OwnedItems[item.DataId];

        // 같은 타입의 장비 이미 있다면 장착 해제
        if (EquippedItems.ContainsKey(item.Type))
        {
            EquippedItems.Remove(item.Type);
        }

        // 새 장비 장착 (OwnedItems의 아이템 참조 사용)
        EquippedItems[item.Type] = ownedItem;

        // 장비 스탯 계산
        CalcEquipItem();

        // 아이템 데이터 저장
        OnEquipmentChanged?.Invoke();

        Debug.Log($"장비 장착 완료 후 플레이어 스탯 - ATK: {player.Atk}, DEF: {player.Def}");
        return true;
    }
    public bool TryUpgrade(int currentGold, Item item)
    {
        if ((item.CurrentLevel >= 1000) || (currentGold < item.UpgradeCost))
        {
            Debug.Log("골드가 부족하거나 최대레벨 상한선");
            return false;
        }

        item.CurrentLevel++;

        if (IsEquipped(item))
        {
            CalcEquipItem();
        }
        //SaveOwnedItems();
        return true;
    }
    #endregion

    #region Save&Load
    public void SaveGame()
    {
        //저장 시점 : 게임 종료시
        SaveStatData();
        SaveOwnedItems();
        SaveSkillData();
        SaveGoldStageData();
    }

    public void loadStageGold()
    {
        GoldStageData loadedGoldStageData = LoadGoldStageData();

        loadedGold = loadedGoldStageData.CurrentStage;
        loadedStage = loadedGoldStageData.CurrentGold;

        if (loadedGoldStageData != null)
        {
            Managers.Instance.Stage.CurrentStageLevel = loadedGoldStageData.CurrentStage == 0 ? 1 : loadedGoldStageData.CurrentStage;
            Managers.Instance.Currency.SetGold(loadedGoldStageData.CurrentGold);
        }
    }

    public void LoadGame()
    {
        // 게임 데이터 로드        
        StatLevelData loadedStatData = LoadStatData();
        if (loadedStatData != null)
        {
            // 로드된 데이터 적용
            Managers.Instance.StatUpgrade.AtkLevel = loadedStatData.AtkLevel == 0 ? 1 : loadedStatData.AtkLevel;
            Managers.Instance.StatUpgrade.HpLevel = loadedStatData.HpLevel == 0 ? 1 : loadedStatData.HpLevel;
            Managers.Instance.StatUpgrade.DefLevel = loadedStatData.DefLevel == 0 ? 1 : loadedStatData.DefLevel;
            Managers.Instance.StatUpgrade.CriRateLevel = loadedStatData.CriRateLevel == 0 ? 1 : loadedStatData.CriRateLevel;
            Managers.Instance.StatUpgrade.criDamageLevel = loadedStatData.CriDamageLevel == 0 ? 1 : loadedStatData.CriDamageLevel;
            Managers.Instance.StatUpgrade.AtkSpeedLevel = loadedStatData.AtkSpeedLevel == 0 ? 1 : loadedStatData.AtkSpeedLevel;
        }
        SkillLevelData loadedSkillData = LoadSkillData();
        if(loadedSkillData != null)
        {
            player.SkillList[0].CurrentLevel = loadedSkillData.Skill_Zoomies_Level;
            player.SkillList[1].CurrentLevel = loadedSkillData.Skill_BakeBread_Level;
            player.SkillList[2].CurrentLevel = loadedSkillData.Skill_EatChur_Level;
            player.SkillList[3].CurrentLevel = loadedSkillData.Skill_BeastEyes_Level;
            player.SkillList[4].CurrentLevel = loadedSkillData.Skill_FatalStrike_Level;
        }

        LoadOwnedItems();
    
    }

    private string GetSavePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, "SaveData", fileName);
    }

    #region saveStatData
    [System.Serializable]
    public class StatLevelData
    {
        public int AtkLevel { get; set; }
        public int HpLevel { get; set; }
        public int DefLevel { get; set; }
        public int CriRateLevel { get; set; }
        public int CriDamageLevel { get; set; }
        public int AtkSpeedLevel { get; set; }
    }

    private void SaveStatData()
    {
        StatLevelData statData = new StatLevelData
        {
            //0이면 1로 해주기
            AtkLevel = Managers.Instance.StatUpgrade.AtkLevel == 0 ? 1 : Managers.Instance.StatUpgrade.AtkLevel,
            HpLevel = Managers.Instance.StatUpgrade.HpLevel == 0 ? 1 : Managers.Instance.StatUpgrade.HpLevel,
            DefLevel = Managers.Instance.StatUpgrade.DefLevel == 0 ? 1 : Managers.Instance.StatUpgrade.DefLevel,
            CriRateLevel = Managers.Instance.StatUpgrade.CriRateLevel == 0 ? 1 : Managers.Instance.StatUpgrade.CriRateLevel,
            CriDamageLevel = Managers.Instance.StatUpgrade.criDamageLevel == 0 ? 1 : Managers.Instance.StatUpgrade.criDamageLevel,
            AtkSpeedLevel = Managers.Instance.StatUpgrade.AtkSpeedLevel == 0 ? 1 : Managers.Instance.StatUpgrade.AtkSpeedLevel,
        };

        string jsonPath = GetSavePath("StatLevelData.json");

        try
        {
            // 디렉토리가 없으면 생성
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));

            // 데이터를 JSON 문자열로 변환
            string jsonStr = JsonConvert.SerializeObject(statData, Formatting.Indented);

            // 파일에 저장
            File.WriteAllText(jsonPath, jsonStr);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
            Debug.Log($"스탯 데이터 저장 완료: {jsonPath}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"스탯 데이터 저장 실패: {e.Message}");
        }
    
    }


    private StatLevelData LoadStatData()
    {
        string jsonPath = GetSavePath("StatLevelData.json");

        try
        {
            if (File.Exists(jsonPath))
            {
                string jsonStr = File.ReadAllText(jsonPath);
                return JsonConvert.DeserializeObject<StatLevelData>(jsonStr);
            }
            else
            {
                Debug.Log("저장된 스탯 데이터가 없습니다. 기본값을 사용합니다.");
                return new StatLevelData(); // 기본값으로 초기화된 객체 반환
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"스탯 데이터 로드 실패: {e.Message}");
            return new StatLevelData();
        }
    }

    #endregion

    #region saveSkillData

    [System.Serializable]
    public class SkillLevelData
    {
        public int Skill_Zoomies_Level { get; set; }
        public int Skill_BakeBread_Level { get; set; }
        public int Skill_BeastEyes_Level { get; set; }
        public int Skill_EatChur_Level { get; set; }
        public int Skill_FatalStrike_Level { get; set; }
    }

    private void SaveSkillData()
    {
        SkillLevelData skillData = new SkillLevelData
        {
            //0이면 1로 해주기
            Skill_Zoomies_Level = player.SkillList[0].CurrentLevel == 0 ? 1 : player.SkillList[0].CurrentLevel,
            Skill_BakeBread_Level = player.SkillList[1].CurrentLevel == 0 ? 1 : player.SkillList[1].CurrentLevel,
            Skill_EatChur_Level = player.SkillList[2].CurrentLevel == 0 ? 1 : player.SkillList[2].CurrentLevel,
            Skill_BeastEyes_Level = player.SkillList[3].CurrentLevel == 0 ? 1 : player.SkillList[3].CurrentLevel,
            Skill_FatalStrike_Level = player.SkillList[4].CurrentLevel == 0 ? 1 : player.SkillList[4].CurrentLevel,
        };

        string jsonPath = GetSavePath("SkillLevelData.json");

        try
        {
            // 디렉토리가 없으면 생성
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));

            // 데이터를 JSON 문자열로 변환
            string jsonStr = JsonConvert.SerializeObject(skillData, Formatting.Indented);

            // 파일에 저장
            File.WriteAllText(jsonPath, jsonStr);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
            Debug.Log($"스탯 데이터 저장 완료: {jsonPath}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"스탯 데이터 저장 실패: {e.Message}");
        }

    }

    private SkillLevelData LoadSkillData()
    {
        string jsonPath = GetSavePath("SkillLevelData.json");

        try
        {
            if (File.Exists(jsonPath))
            {
                string jsonStr = File.ReadAllText(jsonPath);
                var data = JsonConvert.DeserializeObject<SkillLevelData>(jsonStr);
                // 0인 경우 1로 초기화
                if (data.Skill_Zoomies_Level == 0) data.Skill_Zoomies_Level = 1;
                if (data.Skill_BakeBread_Level == 0) data.Skill_BakeBread_Level = 1;
                if (data.Skill_EatChur_Level == 0) data.Skill_EatChur_Level = 1;
                if (data.Skill_BeastEyes_Level == 0) data.Skill_BeastEyes_Level = 1;
                if (data.Skill_FatalStrike_Level == 0) data.Skill_FatalStrike_Level = 1;

                Debug.Log($"스킬 데이터 로드 - Zoomies: {data.Skill_Zoomies_Level}, BakeBread: {data.Skill_BakeBread_Level}");
                return data;
            }
            else
            {
                Debug.Log("저장된 스킬 데이터가 없습니다. 기본값을 사용합니다.");
                return new SkillLevelData
                {
                    Skill_Zoomies_Level = 1,
                    Skill_BakeBread_Level = 1,
                    Skill_EatChur_Level = 1,
                    Skill_BeastEyes_Level = 1,
                    Skill_FatalStrike_Level = 1
                };
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"스킬 데이터 로드 실패: {e.Message}");
            return new SkillLevelData();
        }
    }
    // 스킬 데이터를 캐시로 저장
    private SkillLevelData _cachedSkillData;

    // 스킬 데이터 가져오는 메서드 수정
    public SkillLevelData GetSkillData()
    {
        if (_cachedSkillData == null)
        {
            _cachedSkillData = LoadSkillData();
        }
        return _cachedSkillData;
    }

    public void InitializeSkillLevels(Skill skill)
    {
        var skillData = GetSkillData();

        // 스킬 타입에 따라 저장된 레벨 적용
        if (skill is Skill_Zoomies) skill.CurrentLevel = skillData.Skill_Zoomies_Level;
        else if (skill is Skill_BakeBread) skill.CurrentLevel = skillData.Skill_BakeBread_Level;
        else if (skill is Skill_EatChur) skill.CurrentLevel = skillData.Skill_EatChur_Level;
        else if (skill is Skill_BeastEyes) skill.CurrentLevel = skillData.Skill_BeastEyes_Level;
        else if (skill is Skill_FatalStrike) skill.CurrentLevel = skillData.Skill_FatalStrike_Level;

        // 레벨에 따른 스킬 업데이트
        skill.UpdateSkillByLoadedLevel();

        Debug.Log($"스킬 {skill.GetType().Name} 초기화 완료 - Level: {skill.CurrentLevel}");
    }


    #endregion

    #region saveItemData
    [System.Serializable]
    public class ItemSaveData
    {
        public int DataId { get; set; }
        public int CurrentLevel { get; set; }
        public int CurrentAtk { get; set; }
        public int CurrentDef { get; set; }
    }

    [System.Serializable]
    public class ItemInventorySaveData
    {
        public List<ItemSaveData> OwnedItems { get; set; } = new List<ItemSaveData>();
        public Dictionary<ItemType, int> EquippedItemIds { get; set; } = new Dictionary<ItemType, int>();
    }

    private void SaveOwnedItems()
    {
        var saveData = new ItemInventorySaveData();

        // 보유 아이템 저장
        foreach (var item in OwnedItems.Values)
        {
            saveData.OwnedItems.Add(new ItemSaveData
            {
                DataId = item.DataId,
                CurrentLevel = item.CurrentLevel,
                CurrentAtk = item.CurrentAtk,
                CurrentDef = item.CurrentDef,
            });
        }

        // 장착 중인 아이템 저장
        foreach (var kvp in EquippedItems)
        {
            if (kvp.Value != null)
            {
                saveData.EquippedItemIds[kvp.Key] = kvp.Value.DataId;
            }
        }
        string jsonPath = GetSavePath("ItemLevelData.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
            string jsonStr = JsonConvert.SerializeObject(saveData, Formatting.Indented);
            File.WriteAllText(jsonPath, jsonStr);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
            Debug.Log($"아이템 데이터 저장 완료: {jsonPath}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"아이템 데이터 저장 실패: {e.Message}");
        }
    }
    public event Action OnEquipmentChanged;
    private void LoadOwnedItems()
    {
        string jsonPath = GetSavePath("ItemLevelData.json");

        try
        {
            if (File.Exists(jsonPath))
            {
                string jsonStr = File.ReadAllText(jsonPath);
                var saveData = JsonConvert.DeserializeObject<ItemInventorySaveData>(jsonStr);

                // 보유 아이템 로드
                OwnedItems.Clear();
                foreach (var itemData in saveData.OwnedItems)
                {
                    if (Managers.Instance.Data.ItemDic.TryGetValue(itemData.DataId, out ItemData baseItemData))
                    {
                        Item item = new Item(baseItemData);
                        item.LoadFromSaveData(itemData);
                        OwnedItems.Add(item.DataId, item);
                    }
                }

                // 장착 아이템 로드
                EquippedItems.Clear();
                foreach (var kvp in saveData.EquippedItemIds)
                {
                    if (OwnedItems.TryGetValue(kvp.Value, out Item item))
                    {
                        EquippedItems[kvp.Key] = item;
                    }
                }

                // 장비 스탯 재계산
                CalcEquipItem();

                // UI 업데이트 알림
                OnEquipmentChanged?.Invoke();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"아이템 데이터 로드 실패: {e.Message}");
        }
    }

    #endregion

    #region Gold&StageData
    [System.Serializable]
    public class GoldStageData
    {
        public int CurrentGold { get; set; }
        public int CurrentStage { get; set; }
    }

    private void SaveGoldStageData()
    {
        GoldStageData goldStageData = new GoldStageData
        {
            //0이면 1로 해주기
            CurrentGold = Managers.Instance.Currency.GetCurrentGold(),
            CurrentStage = Managers.Instance.Stage.GetCurrentStageLevel(),
        };
        string jsonPath = GetSavePath("GoldStageData.json");

        try
        {
            // 디렉토리가 없으면 생성
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));

            // 데이터를 JSON 문자열로 변환
            string jsonStr = JsonConvert.SerializeObject(goldStageData, Formatting.Indented);

            // 파일에 저장
            File.WriteAllText(jsonPath, jsonStr);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
            Debug.Log($"골드&스테이지 데이터 저장 완료: {jsonPath}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"골드&스테이지 저장 실패: {e.Message}");
        }
    }
    private GoldStageData LoadGoldStageData()
    {
        string jsonPath = GetSavePath("GoldStageData.json");

        try
        {
            if (File.Exists(jsonPath))
            {
                string jsonStr = File.ReadAllText(jsonPath);
                return JsonConvert.DeserializeObject<GoldStageData>(jsonStr);
            }
            else
            {
                Debug.Log("저장된 스탯 데이터가 없습니다. 기본값을 사용합니다.");
                return new GoldStageData(); // 기본값으로 초기화된 객체 반환
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"스탯 데이터 로드 실패: {e.Message}");
            return new GoldStageData();
        }
    }

    #endregion

    #endregion

}

