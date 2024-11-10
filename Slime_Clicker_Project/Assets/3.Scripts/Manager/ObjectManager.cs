using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using static DataManager;
using static UnityEngine.RuleTile.TilingRuleOutput;


public class ObjectManager
{
    public Dictionary<int, CreatureData> CreatureDataDic;
    public Player _player { get; private set; }
    public HashSet<Monster> Monsters { get; } = new HashSet<Monster>();
    public HashSet<Projectile> Projectiles { get; } = new HashSet<Projectile>();

    // ��Ģ : ��� ������Ʈ�� ������ spawn���� �����ؾ� �Ѵ�.
    // TODO : ��� ��� dataid �߰���
    public T Spawn<T>(Vector3 position, int DataId = 0, string prefabName = "") where T : BaseObject
    {
        Type type = typeof(T);

        //�ѹ��� ������ ��Ʈ �����ؼ� �б�
        if (CreatureDataDic == null)
        {
            CreatureDataDic = Managers.Instance.Data.CreatureDic;
        }

        if (type == typeof(Player)) // Player ����
        {
            CreatureData playerData;
            if (CreatureDataDic.TryGetValue(DataId, out playerData))
            {
                GameObject go = Managers.Instance.Resource.Instantiate("Player");
                Player player = go.GetOrAddComponent<Player>();
                player.transform.position = position;
                player.SetInfo(DataId);
                _player = player;
                player.Initialize();
                Managers.Instance.Game.player = player;
                return player as T;
            }
            else
            {
                Debug.LogError("�÷��̾� ������ ���� ���߽��ϴ�. Wrong DatId or Not Exist in DataTable");
                return null;
            }
        }

        else if (type == typeof(Monster)) // Monster ����
        {
            CreatureData mosnterData;
            if (CreatureDataDic.TryGetValue(DataId, out mosnterData))
            {
                // ���ʹ� ��� ���� Mosnter �������� ���. ���⼭ ���Ҵ��ؼ� ����Ѵ�!
                GameObject go = Managers.Instance.Resource.Instantiate("Monster", pooling: true);
                Monster monster = go.GetOrAddComponent<Monster>();
                monster.transform.position = position;
                monster.SetInfo(DataId);
                monster.GetComponent<SpriteRenderer>().sprite = Managers.Instance.Resource.Load<Sprite>(mosnterData.SpriteName);
                monster.Initialize();
                Monsters.Add(monster);
                return monster as T;
            }
            else
            {
                Debug.LogError("���� ������ ���� ���߽��ϴ�. Wrong DatId or Not Exist in DataTable");
                return null;
            }
        }
        else if (type == typeof(Projectile))
        {
            GameObject go = Managers.Instance.Resource.Instantiate(prefabName, pooling: true);
            Projectile projectile = go.GetOrAddComponent<Projectile>();
            go.transform.position = position;
            Projectiles.Add(projectile);
            projectile.Initialize();
            return projectile as T;
        }
       
        else if (type == typeof(BaseObject))
        {
            GameObject go = Managers.Instance.Resource.Instantiate(prefabName, pooling: true);
            go.transform.position = position;
            return go as T;
        }

        Debug.LogError("Wrong DatId or Not Exist in DataTable");
        return null;
    }

    public void Despawn<T>(T obj) where T : BaseObject
    {
        System.Type type = typeof(T);

        if (type == typeof(Player))
        {
        }
        else if (type == typeof(Monster))
        {
            //Monster.Remove(obj as Monster);
            Managers.Instance.Resource.Destroy(obj.gameObject);
        }
        else if (type == typeof(Projectile))
        {
            //Projectiles.Remove(obj as ProjectileController);
            Managers.Instance.Resource.Destroy(obj.gameObject);
        }
    }

    public void ShowDamageFont(Vector2 pos, float damage, float healAmount, UnityEngine.Transform parent, bool isCritical = false)
    {
        string prefabName;
        if (isCritical)
        {
            prefabName = "CriticalDamageText";
        }
        else
        {
            prefabName = "DamageText";
        }
        GameObject go = Managers.Instance.Resource.Instantiate(prefabName, pooling: true);
        ShowDamage damageText = go.GetOrAddComponent<ShowDamage>();
        damageText.SetInfo(pos, damage, healAmount, parent, isCritical);
    }

    public void ShowEffect(Vector2 pos, string name)
    {
        string prefabName = name;
        GameObject go = Managers.Instance.Resource.Instantiate(prefabName, pooling: true);
        EffectBase effect = go.GetOrAddComponent<EffectBase>();
        effect.SetInfo(pos);
    }
}