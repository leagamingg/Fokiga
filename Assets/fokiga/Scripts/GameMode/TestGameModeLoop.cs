using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestGameModeLoop : MonoBehaviour
{
    private const string CHARACTER_PREFAB_PATH = "Character/unitychan";
    public GameObject SpawnPoint;
    public GameObject CharacterLayer;

    private void Start()
    {
        SpawnCharacterOnGameStart();
    }

    private void SpawnCharacterOnGameStart()
    {
        // 先判断生成点是否有效
        if (SpawnPoint == null)
        {
            Debug.LogError("SpawnPoint未赋值！请在Inspector中设置生成点物体");
            return;
        }

        // 加载角色预制体
        GameObject characterPrefab = Resources.Load<GameObject>(CHARACTER_PREFAB_PATH);
        if (characterPrefab == null)
        {
            Debug.LogError($"无法加载角色预制体，路径：{CHARACTER_PREFAB_PATH}");
            return;
        }

        // 检查CharacterManager是否存在
        if (CharacterManager.Instance == null)
        {
            Debug.LogError("CharacterManager实例不存在，无法创建角色");
            return;
        }

        // 创建角色（这里假设CreateCharacter的第二个参数是父物体，若不需要父物体可传null）
        CharacterActor character = CharacterManager.Instance.CreateCharacter(characterPrefab, CharacterLayer.transform);
        if (character != null)
        {
            character.RealObject.transform.position = SpawnPoint.transform.position;
            character.Activate();
            Debug.Log("角色已在SpawnPoint位置生成成功");
        }
        else
        {
            Debug.LogError("角色创建失败");
        }
    }
}