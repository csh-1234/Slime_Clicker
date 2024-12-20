using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Enums;

public class StageManager
{
    public int CurrentStageLevel { get; set; } = 1;  // 스테이지 레벨
    public float StageTime { get; set; } = 120f;      // 한 스테이지당 시간(초)

    public List<Monster> StageMonster = new List<Monster>();
    public float DifficultyByLevel      //래밸당 난이도(몬스터 스텟에 합산)
    {
        get 
        {
            float baseMultiplier = 1.0f;
            float exponentialGrowth = Mathf.Pow(1.015f, CurrentStageLevel - 1); // 난이도 배율 설정
            float finalMultiplier = baseMultiplier * exponentialGrowth;
            return baseMultiplier * exponentialGrowth;
        }
    }

    public int GetCurrentStageLevel()
    {
        return CurrentStageLevel;
    }

    public void AddCurrentStageLevel()
    {
        CurrentStageLevel++;
    }

    public void ClearCurrentStage()
    {
        if (Managers.Instance.Game.MonsterList.Count != 0)
        {
            foreach (Monster monster in Managers.Instance.Game.MonsterList)
            {
                Managers.Instance.Object.Despawn(monster);
            }
        }
        Managers.Instance.Game.MonsterList.Clear();
        StageMonster.Clear();  // StageMonster 리스트도 클리어
    }

    public void StartStage()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 spawnPos = (Vector2)Managers.Instance.Game.player.transform.position + new Vector2(Random.Range(4f, 20f), Random.Range(-1f, 1f));

            int monsterDataID = 0;
            int monsterSelect = Random.Range(1, 4);
            if(monsterSelect == 1) { monsterDataID = (int)EDataId.Slime_Tanker; }
            else if(monsterSelect == 2) { monsterDataID = (int)EDataId.Slime_Attacker; }
            else if(monsterSelect == 3) { monsterDataID = (int)EDataId.Slime_Ranger; }
            
            //Monster mo = Managers.Instance.Object.Spawn<Monster>(spawnPos, monsterDataID);
            Monster mo = Managers.Instance.Object.Spawn<Monster>(spawnPos, monsterDataID);
            mo._currentStats.Hp = (int)DifficultyByLevel * mo._baseStats.Hp;
            mo._currentStats.MaxHp = (int)DifficultyByLevel * mo._baseStats.MaxHp;
            mo._currentStats.Attack = (int)DifficultyByLevel * mo._baseStats.Attack;
            mo._currentStats.Defense = (int)DifficultyByLevel * mo._baseStats.Defense;
            Debug.Log($"배율 : {DifficultyByLevel}");
            Managers.Instance.Game.MonsterList.Add(mo);
            StageMonster.Add(mo);
        }
    }
}
