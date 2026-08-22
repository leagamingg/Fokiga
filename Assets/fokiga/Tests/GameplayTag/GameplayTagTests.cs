#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using Fokiga.Runtime.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fokiga.Tests
{
    public sealed class GameplayTagTests
    {
        private GameplayTagDatabase mDatabase;
        private bool mOwnsDatabase;

        [SetUp]
        public void SetUp()
        {
            if (Application.isPlaying)
            {
                mDatabase = Resources.Load<GameplayTagDatabase>("GameplayTags");
                Assert.IsNotNull(mDatabase);
                mOwnsDatabase = false;
                return;
            }

            mDatabase = ScriptableObject.CreateInstance<GameplayTagDatabase>();
            mOwnsDatabase = true;
            var character = mDatabase.AddRoot("Character");
            var player = mDatabase.AddChild("Player", character.Guid);
            mDatabase.AddChild("Melee", player.Guid);
            mDatabase.AddChild("Ranged", player.Guid);
            var enemy = mDatabase.AddChild("Enemy", character.Guid);
            mDatabase.AddChild("Boss", enemy.Guid);

            Assert.IsTrue(GameplayTagRegistry.Initialize(mDatabase));
        }

        [TearDown]
        public void TearDown()
        {
            if (mOwnsDatabase)
            {
                Object.DestroyImmediate(mDatabase);
            }
        }

        [Test]
        public void ResolvesParentAndChildRelationships()
        {
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character", out var character));
            Assert.IsTrue(GameplayTagRegistry.IsChildOf(melee, character));
            Assert.IsTrue(GameplayTagRegistry.IsChildOf(character, character));
            Assert.IsFalse(GameplayTagRegistry.IsChildOf(character, character, false));
            Assert.IsFalse(GameplayTagRegistry.IsChildOf(character, melee));
            Assert.AreEqual("Character.Player", GameplayTagRegistry.GetParent(melee).Path);
            Assert.AreEqual(2, GameplayTagRegistry.GetChildren(character).Count);
            Assert.AreEqual(2, GameplayTagRegistry.GetAncestors(melee).Count);
            Assert.AreEqual(5, GameplayTagRegistry.GetDescendants(character).Count);
        }

        [Test]
        public void ContainerMatchesCategoriesAndExactTags()
        {
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player", out var player));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character", out var character));

            var container = new GameplayTagContainer();
            Assert.IsTrue(container.Add(melee));
            Assert.IsTrue(container.HasTagExact(melee));
            Assert.IsFalse(container.HasTagExact(player));
            Assert.IsTrue(container.HasTag(player));
            Assert.IsTrue(container.HasTag(character));

            Assert.IsTrue(container.Remove(melee));
            Assert.IsFalse(container.HasTag(character));
        }

        [Test]
        public void SharedParentReferencesSurviveRemovingOneTag()
        {
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Ranged", out var ranged));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player", out var player));

            var container = new GameplayTagContainer();
            container.Add(melee);
            container.Add(ranged);
            container.Remove(melee);

            Assert.IsTrue(container.HasTag(player));
            Assert.IsTrue(container.HasTag(ranged));
            container.Remove(ranged);
            Assert.IsFalse(container.HasTag(player));
        }

        [Test]
        public void AddAndRemoveAreIdempotentAndClearRemovesAllCategories()
        {
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character", out var character));

            var container = new GameplayTagContainer();
            Assert.IsTrue(container.Add(melee));
            Assert.IsFalse(container.Add(melee));
            Assert.AreEqual(1, container.Count);
            Assert.IsFalse(container.Remove(character));
            container.Clear();
            Assert.AreEqual(0, container.Count);
            Assert.IsFalse(container.HasTag(character));
            Assert.IsFalse(container.Remove(melee));
        }

        [Test]
        public void AnyAndAllUseCategorySemantics()
        {
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Ranged", out var ranged));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Enemy.Boss", out var boss));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player", out var player));

            var container = new GameplayTagContainer();
            container.Add(melee);
            Assert.IsTrue(container.HasAny(new[] { boss, player }));
            Assert.IsFalse(container.HasAny(new[] { boss }));
            Assert.IsFalse(container.HasAll(new[] { melee, ranged }));
            Assert.IsTrue(container.HasAll(new[] { player, melee }));
            Assert.IsFalse(container.HasAny(null));
            Assert.IsFalse(container.HasAll(null));
        }

        [Test]
        public void RenameKeepsGuidLookupStable()
        {
            var player = mDatabase.Nodes[1];
            var guid = player.Guid;
            Assert.IsTrue(mDatabase.Rename(guid, "User"));
            Assert.IsTrue(GameplayTagRegistry.Initialize(mDatabase));
            Assert.IsTrue(GameplayTagRegistry.TryGetTagByGuid(guid, out var renamed));
            Assert.AreEqual("Character.User", renamed.Path);
            Assert.IsFalse(GameplayTagRegistry.TryGetTag("Character.Player", out _));
        }

        [Test]
        public void UnknownTagIsRejectedWithoutChangingContainer()
        {
            var container = new GameplayTagContainer();
            Assert.IsFalse(container.Add("Character.Unknown"));
            Assert.AreEqual(0, container.Count);
            Assert.IsFalse(container.HasTag(GameplayTag.Invalid));
        }

        [Test]
        public void HotCategoryQueriesUseCachedBitset()
        {
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Melee", out var melee));
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character", out var character));
            var container = new GameplayTagContainer();
            container.Add(melee);

            var matched = 0;
            for (var index = 0; index < 1000; index++)
            {
                if (container.HasTag(character))
                {
                    matched++;
                }
            }

            for (var index = 0; index < 100000; index++)
            {
                if (container.HasTag(character))
                {
                    matched++;
                }
            }

            Assert.AreEqual(101000, matched);
        }

        [Test]
        public void ValidationReportsDuplicatePathsAndCycles()
        {
            var duplicate = mDatabase.AddRoot("Character");
            var report = mDatabase.Validate();
            Assert.IsFalse(report.IsValid);
            Assert.That(report.Errors, Has.Some.Contains("重复的标签路径"));

            var first = mDatabase.AddRoot("CycleA");
            var second = mDatabase.AddChild("CycleB", first.Guid);
            mDatabase.Reparent(first.Guid, second.Guid);
            report = mDatabase.Validate();
            Assert.IsFalse(report.IsValid);
            Assert.That(report.Errors, Has.Some.Contains("循环关系"));
            Assert.IsNotNull(duplicate);
        }

        [Test]
        public void ValidationRejectsMalformedGuids()
        {
            var database = ScriptableObject.CreateInstance<GameplayTagDatabase>();
            database.AddRoot("Valid");
            var nodes = database.Nodes as List<GameplayTagNodeData>;
            Assert.IsNotNull(nodes);
            nodes.Add(new GameplayTagNodeData("not-a-guid", "Invalid", string.Empty));

            var report = database.Validate();
            Assert.IsFalse(report.IsValid);
            Assert.That(report.Errors, Has.Some.Contains("32 位十六进制 GUID"));
            Object.DestroyImmediate(database);
        }

        [UnityTest]
        public IEnumerator ComponentLoadsSerializedDefaultTags()
        {
            var database = Resources.Load<GameplayTagDatabase>("GameplayTags");
            Assert.IsNotNull(database);
            Assert.IsTrue(GameplayTagRegistry.Initialize(database));

            var prefab = Resources.Load<GameObject>("Character/unitychan");
            Assert.IsNotNull(prefab);
            var gameObject = Object.Instantiate(prefab);
            var component = gameObject.GetComponent<GameplayTagComponent>();
            Assert.IsNotNull(component);
            Assert.IsTrue(component.HasTagExact("Character.Player.Melee"));
            Assert.IsTrue(component.HasTag("Character"));

            Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentKeepsRuntimeTagsWhenReenabled()
        {
            Assert.IsTrue(GameplayTagRegistry.TryGetTag("Character.Player.Ranged", out var ranged));

            var gameObject = new GameObject("GameplayTagTestObject");
            var component = gameObject.AddComponent<GameplayTagComponent>();
            Assert.IsTrue(component.AddTag(ranged));

            gameObject.SetActive(false);
            gameObject.SetActive(true);

            Assert.IsTrue(component.HasTagExact(ranged));
            Object.Destroy(gameObject);
            yield return null;
        }
    }
}
#endif
