using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色管理器，继承自ManagerBase，专门负责管理游戏中的角色实体CharacterActor
/// 实现单例模式，全局唯一实例
/// </summary>
public class CharacterManager : ManagerBase
{
    // 单例实例
    public static CharacterManager Instance { get; private set; }

    /// <summary>
    /// 确保单例唯一并初始化
    /// </summary>
    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 切换场景时不销毁
        base.Awake();
    }

    /// <summary>
    /// 生成角色专属唯一ID（包含角色类型前缀）
    /// </summary>
    protected virtual string GenerateCharacterId()
    {
        // 统一使用nameof获取类型名，避免硬编码和typeof的冗余
        string typePrefix = nameof(CharacterActor);
        return $"{typePrefix}_{GenerateUniqueId()}";
    }

    /// <summary>
    /// 从预制体创建角色
    /// </summary>
    public virtual CharacterActor CreateCharacter(GameObject characterPrefab, Transform parent = null, string actorId = null)
    {
        string id = string.IsNullOrEmpty(actorId) ? GenerateCharacterId() : actorId;
        return CreateActorFromPrefab<CharacterActor>(characterPrefab, parent, id);
    }

    /// <summary>
    /// 从现有GameObject创建角色
    /// </summary>
    public virtual CharacterActor CreateCharacter(GameObject existingObject, string actorId = null)
    {
        string id = string.IsNullOrEmpty(actorId) ? GenerateCharacterId() : actorId;
        return CreateActorFromExisting<CharacterActor>(existingObject, id);
    }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    public virtual List<CharacterActor> GetAllCharacters()
    {
        return GetActorsOfType<CharacterActor>();
    }

    /// <summary>
    /// 移除指定角色
    /// </summary>
    public virtual bool RemoveCharacter(string characterId)
    {
        // 使用nameof统一类型检查，与ID生成逻辑保持一致
        if (characterId != null && !characterId.StartsWith(nameof(CharacterActor)))
        {
            Debug.LogWarning($"移除失败：ID {characterId} 不是角色ID");
            return false;
        }
        return RemoveActor(characterId);
    }

    /// <summary>
    /// 销毁时清理单例
    /// </summary>
    protected override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        base.OnDestroy();
    }
}