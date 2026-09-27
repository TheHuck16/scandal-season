// UNITY-DEPENDENT — EditMode tests. Run inside the Unity Editor via
// Window > General > Test Runner, and in CI via game-ci/unity-test-runner.
// They assert the locked Season One story container against the generated
// SceneDefinitionSO assets (Assets/Scripts/Runtime/Generated/Scenes):
// 30 chapters x 40 scenes, exactly 3 key decisions / 1 ritual / 1 sting /
// 1 cliffhanger per chapter. Content source: Content/scenes.json (schema v2).

using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace ScandalSeason.Tests.EditMode
{
    public sealed class SceneContentEditModeTests
    {
        private const string ScenesRoot = "Assets/Scripts/Runtime/Generated/Scenes";

        // Unity's serializer instantiates nested [Serializable] objects on load even
        // when they were null at save time, so presence is detected by content —
        // the same convention ContentImporter uses at import time.
        private static bool HasKeyDecision(SceneDefinitionSO s) =>
            s.keyDecision != null && !string.IsNullOrWhiteSpace(s.keyDecision.title);
        private static bool HasRitual(SceneDefinitionSO s) =>
            s.ritual != null && !string.IsNullOrWhiteSpace(s.ritual.occasionBrief);

        private static SceneDefinitionSO[] LoadAllScenes() =>
            AssetDatabase.FindAssets("t:SceneDefinitionSO", new[] { ScenesRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneDefinitionSO>)
                .Where(so => so != null)
                .ToArray();

        [Test]
        public void SeasonOneHas1200Scenes()
        {
            var scenes = LoadAllScenes();
            Assert.AreEqual(30 * 40, scenes.Length,
                $"Expected the full 1,200-scene Season One container, found {scenes.Length}.");
        }

        [Test]
        public void EveryChapterHas40ScenesNumbered1To40()
        {
            var scenes = LoadAllScenes();
            foreach (var chapter in scenes.Select(s => s.chapter).Distinct().OrderBy(c => c))
            {
                var numbers = scenes.Where(s => s.chapter == chapter)
                    .Select(s => s.sceneNumber).OrderBy(n => n).ToArray();
                Assert.AreEqual(Enumerable.Range(1, 40), numbers,
                    $"Chapter {chapter} scene numbers are not 1–40.");
            }
        }

        [Test]
        public void EveryChapterHasExactly3KeyDecisions()
        {
            var scenes = LoadAllScenes();
            foreach (var chapter in scenes.Select(s => s.chapter).Distinct().OrderBy(c => c))
            {
                var decisions = scenes.Where(s => s.chapter == chapter && HasKeyDecision(s))
                    .Select(s => s.keyDecision.number).OrderBy(n => n).ToArray();
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, decisions,
                    $"Chapter {chapter} must carry exactly key decisions 1, 2, 3.");
                foreach (var kd in scenes.Where(s => s.chapter == chapter && HasKeyDecision(s))
                    .Select(s => s.keyDecision))
                {
                    Assert.GreaterOrEqual(kd.options.Length, 2,
                        $"Chapter {chapter} key decision '{kd.title}' needs at least 2 options.");
                }
            }
        }

        [Test]
        public void EveryChapterHasOneRitualOneStingOneCliffhanger()
        {
            var scenes = LoadAllScenes();
            foreach (var chapter in scenes.Select(s => s.chapter).Distinct().OrderBy(c => c))
            {
                var inChapter = scenes.Where(s => s.chapter == chapter).ToArray();
                Assert.AreEqual(1, inChapter.Count(s => s.type == SceneType.ChapterClimax),
                    $"Chapter {chapter} must have exactly 1 dressing ritual.");
                Assert.AreEqual(1, inChapter.Count(s => s.type == SceneType.GazetteSting),
                    $"Chapter {chapter} must have exactly 1 Gazette sting.");
                Assert.AreEqual(1, inChapter.Count(s => s.type == SceneType.Cliffhanger),
                    $"Chapter {chapter} must have exactly 1 cliffhanger.");
            }
        }

        [Test]
        public void RitualsCarryOccasionBriefAndCoinCosts()
        {
            var rituals = LoadAllScenes().Where(HasRitual).ToArray();
            Assert.AreEqual(30, rituals.Length, "Expected one dressing ritual per chapter.");
            foreach (var scene in rituals)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(scene.ritual.occasionBrief),
                    $"Ritual {scene.sceneId} needs its occasion brief.");
                Assert.Greater(scene.ritual.directions.Length, 0,
                    $"Ritual {scene.sceneId} needs directions.");
                Assert.Greater(scene.ritual.coinPerDecision, 0,
                    $"Ritual {scene.sceneId} needs a coin-per-decision cost.");
            }
        }

        [Test]
        public void CustomAnimationIsFlaggedWithAReason()
        {
            var custom = LoadAllScenes().Where(s => s.animation == SceneAnimation.Custom).ToArray();
            foreach (var scene in custom)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(scene.animationNote),
                    $"Custom animation on {scene.sceneId} must carry its bespoke reason.");
            }
        }
    }
}
