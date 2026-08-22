# GameplayTag

GameplayTag 是一套独立的层级标签系统，不会替换 Unity 自带的 TagManager。

## 配置

默认数据库位于 `Assets/fokiga/Resources/GameplayTags.asset`，可通过 Unity 菜单 `工具/GameplayTag 标签` 打开编辑器。默认标签如下：

```text
Character
  Character.Player
    Character.Player.Melee
    Character.Player.Ranged
  Character.Enemy
    Character.Enemy.Boss
```

给 GameObject 或 Prefab 添加 `GameplayTagComponent` 即可配置多个标签。组件保存的是稳定 GUID，因此标签重命名或移动后，Prefab 和场景引用仍然有效。

## 身份与 ID

每个标签使用 32 位十六进制 GUID 作为持久化身份。这与 Unity 常用的资源 GUID 格式一致，长度很小，适合保存到场景、Prefab 和存档引用中。标签重命名、移动或排序时，GUID 都不会改变。

`GameplayTagId` 是 `GameplayTagRegistry` 生成的运行时整数 ID，用于位集合和快速查询。它只在当前注册表生命周期内有效，不能保存到场景、Prefab 或存档中。编辑器会在身份详情中显示当前运行时 ID，方便调试。

## 运行时使用

```csharp
using Fokiga.Runtime.Gameplay;

GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee);

var tags = new GameplayTagContainer();
tags.Add(melee);

bool exact = tags.HasTagExact(melee);
bool belongsToCharacter = GameplayTagRegistry.TryGetTag("Character", out var character)
    && tags.HasTag(character);
```

`HasTagExact` 只匹配对象明确添加的标签；`HasTag` 还会匹配子标签。例如对象拥有 `Character.Player.Melee` 时，也会匹配 `Character.Player` 和 `Character`。

Actor 的真实 GameObject 上挂有 `GameplayTagComponent` 时，可以使用扩展方法：

```csharp
actor.HasGameplayTag(character);
actor.AddGameplayTag(melee);
actor.RemoveGameplayTag(melee);
```

注册表会在首个场景加载前编译数据库。运行时标签定义只读，但对象上的标签可以动态增删。

## 编辑器操作

打开 `工具/GameplayTag 标签` 后，可以创建根标签和子标签、重命名、移动节点、搜索完整路径、复制路径或 GUID、查看当前运行时 ID、校验数据库以及查找资源引用。

`GameplayTagComponent` Inspector 使用相同的树形选择器，支持搜索和展开/折叠。重复标签会自动去重，未知 GUID 会保留并提示，避免错误引用被静默删除。
