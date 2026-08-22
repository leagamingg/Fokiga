using Fokiga.GameplayTags;

namespace Fokiga.GameplayTags
{
    public static class GameplayTagActorExtensions
    {
        public static GameplayTagComponent GetGameplayTagComponent(this ActorBase actor)
        {
            return actor?.RealObject != null
                ? actor.RealObject.GetComponent<GameplayTagComponent>()
                : null;
        }

        public static bool HasGameplayTag(this ActorBase actor, GameplayTag tag)
        {
            return actor.GetGameplayTagComponent()?.HasTag(tag) == true;
        }

        public static bool HasGameplayTagExact(this ActorBase actor, GameplayTag tag)
        {
            return actor.GetGameplayTagComponent()?.HasTagExact(tag) == true;
        }

        public static bool AddGameplayTag(this ActorBase actor, GameplayTag tag)
        {
            return actor.GetGameplayTagComponent()?.AddTag(tag) == true;
        }

        public static bool RemoveGameplayTag(this ActorBase actor, GameplayTag tag)
        {
            return actor.GetGameplayTagComponent()?.RemoveTag(tag) == true;
        }
    }
}
