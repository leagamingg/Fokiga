# GameplayTag

GameplayTag is an additional hierarchical tag system. It does not replace Unity's built-in TagManager.

## Setup

The default database is `Assets/fokiga/Resources/GameplayTags.asset`. Open `Tools/Gameplay Tags` to edit it. The initial database contains:

```text
Character
  Character.Player
    Character.Player.Melee
    Character.Player.Ranged
  Character.Enemy
    Character.Enemy.Boss
```

Add `GameplayTagComponent` to a GameObject or prefab to assign multiple tags. References are stored by stable GUID, so renaming or moving a tag keeps prefab references valid.

## Runtime usage

```csharp
using Fokiga.GameplayTags;

GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee);

var tags = new GameplayTagContainer();
tags.Add(melee);

bool exact = tags.HasTagExact(melee);
bool belongsToCharacter = GameplayTagRegistry.TryGetTag("Character", out var character)
    && tags.HasTag(character);
```

`HasTagExact` only matches an explicitly assigned tag. `HasTag` also matches descendants, so a `Character.Player.Melee` tag matches `Character.Player` and `Character`.

For an Actor backed by a GameObject with `GameplayTagComponent`, use the extension methods in `Fokiga.GameplayTags`:

```csharp
actor.HasGameplayTag(character);
actor.AddGameplayTag(melee);
actor.RemoveGameplayTag(melee);
```

The registry compiles the database before the first scene. Tag definitions are immutable after runtime initialization; object containers remain mutable.
