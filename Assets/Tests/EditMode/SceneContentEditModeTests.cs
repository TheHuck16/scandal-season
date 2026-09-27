// UNITY-DEPENDENT — EditMode tests. Run inside the Unity Editor via
// Window > General > Test Runner, and in CI via game-ci/unity-test-runner.
// They assert the locked story containers against the generated
// SceneDefinitionSO assets (Assets/Scripts/Runtime/Generated/Scenes):
// per season, 30 chapters x 40 scenes, exactly 3 key decisions / 1 contiguous
// dressing-ritual group / 1 sting / 1 cliffhanger per chapter.
// Content sources: Content/scenes.json + Content/scenes-season2.json (schema v2.1).

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
            s.ritual != null && (!string.IsNullOrWhiteSpace(s.ritual.occasionBrief)
                || (s.ritual.directions != null && s.ritual.directions.Length > 0)
                || (s.ritual.steps != null && s.ritual.steps.Length > 0)
                || !string.IsNullOrWhiteSpace(s.ritual.part));

        private static SceneDefinitionSO[] LoadAllScenes() =>
            AssetDatabase.FindAssets("t:SceneDefinitionSO", new[] { ScenesRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneDefinitionSO>)
                .Where(so => so != null)
                .ToArray();

        private static int CountRitualGroups(SceneDefinitionSO[] inChapter)
        {
            var numbers = inChapter.Where(s => s.type == SceneType.ChapterClimax)
                .Select(s => s.sceneNumber).OrderBy(n => n).ToArray();
            int groups = 0, prev = -2;
            foreach (var n in numbers)
            {
                if (n != prev + 1) groups++;
                prev = n;
            }
            return groups;
        }

        [Test]
        public void EachSeasonHas1200Scenes()
        {
            var scenes = LoadAllScenes();
            foreach (var season in scenes.Select(s => s.season).Distinct().OrderBy(x => x))
            {
                int count = scenes.Count(s => s.season == season);
                Assert.AreEqual(30 * 40, count,
                    $"Expected the full 1,200-scene Season {season} container, found {count}.");
            }
        }

        [Test]
        public void TotalSceneCountTripwire()
        {
            var scenes = LoadAllScenes();
            int seasons = scenes.Select(s => s.season).Distinct().Count();
            Assert.AreEqual(seasons * 30 * 40, scenes.Length,
                $"Expected {seasons} seasons x 1,200 scenes = {seasons * 1200}, found {scenes.Length}.");
        }

        [Test]
        public void EveryChapterHas40ScenesNumbered1To40()
        {
            var scenes = LoadAllScenes();
            foreach (var key in scenes.Select(s => (s.season, s.chapter)).Distinct().OrderBy(k => k))
            {
                var numbers = scenes.Where(s => s.season == key.season && s.chapter == key.chapter)
                    .Select(s => s.sceneNumber).OrderBy(n => n).ToArray();
                Assert.AreEqual(Enumerable.Range(1, 40), numbers,
                    $"S{key.season} chapter {key.chapter} scene numbers are not 1–40.");
            }
        }

        [Test]
        public void EveryChapterHasExactly3KeyDecisions()
        {
            var scenes = LoadAllScenes();
            foreach (var key in scenes.Select(s => (s.season, s.chapter)).Distinct().OrderBy(k => k))
            {
                var inChapter = scenes.Where(s => s.season == key.season && s.chapter == key.chapter).ToArray();
                var decisions = inChapter.Where(HasKeyDecision)
                    .Select(s => s.keyDecision.number).OrderBy(n => n).ToArray();
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, decisions,
                    $"S{key.season} chapter {key.chapter} must carry exactly key decisions 1, 2, 3.");
                foreach (var kd in inChapter.Where(HasKeyDecision).Select(s => s.keyDecision))
                {
                    Assert.GreaterOrEqual(kd.options.Length, 2,
                        $"S{key.season} chapter {key.chapter} key decision '{kd.title}' needs at least 2 options.");
                }
            }
        }

        [Test]
        public void EveryChapterHasOneRitualGroupOneStingOneCliffhanger()
        {
            var scenes = LoadAllScenes();
            foreach (var key in scenes.Select(s => (s.season, s.chapter)).Distinct().OrderBy(k => k))
            {
                var inChapter = scenes.Where(s => s.season == key.season && s.chapter == key.chapter).ToArray();
                Assert.AreEqual(1, CountRitualGroups(inChapter),
                    $"S{key.season} chapter {key.chapter} must have exactly 1 contiguous dressing-ritual group.");
                Assert.AreEqual(1, inChapter.Count(s => s.type == SceneType.GazetteSting),
                    $"S{key.season} chapter {key.chapter} must have exactly 1 Gazette sting.");
                Assert.AreEqual(1, inChapter.Count(s => s.type == SceneType.Cliffhanger),
                    $"S{key.season} chapter {key.chapter} must have exactly 1 cliffhanger.");
            }
        }

        [Test]
        public void RitualGroupsCarryOccasionBriefAndCoinCosts()
        {
            var scenes = LoadAllScenes();
            foreach (var key in scenes.Select(s => (s.season, s.chapter)).Distinct().OrderBy(k => k))
            {
                var parts = scenes.Where(s => s.season == key.season && s.chapter == key.chapter
                    && s.type == SceneType.ChapterClimax && HasRitual(s)).ToArray();
                Assert.Greater(parts.Length, 0,
                    $"S{key.season} chapter {key.chapter} ritual group has no ritual content.");
                Assert.IsTrue(parts.Any(p => !string.IsNullOrWhiteSpace(p.ritual.occasionBrief)),
                    $"S{key.season} chapter {key.chapter} ritual group needs at least one occasion brief.");
                Assert.IsTrue(parts.Any(p => p.ritual.coinPerDecision > 0),
                    $"S{key.season} chapter {key.chapter} ritual group needs a coin-per-decision cost.");
            }
        }

        [Test]
        public void CustomAnimationIsFlaggedWithAReason()
        {
            var custom = LoadAllScenes().Where(s => s.animation == SceneAnimation.Custom).ToArray();
            Assert.Greater(custom.Length, 0, "Expected some Custom animation calls.");
            foreach (var scene in custom)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(scene.animationNote),
                    $"Custom animation on {scene.sceneId} must carry its bespoke reason.");
            }
        }

        [Test]
        public void CustomAnimationIsTentpoleOnly()
        {
            var scenes = LoadAllScenes();
            var s1CustomChapters = scenes.Where(s => s.season == 1 && s.animation == SceneAnimation.Custom)
                .Select(s => s.chapter).Distinct().OrderBy(c => c).ToArray();
            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, s1CustomChapters,
                $"Season One Custom animation must sit only in tentpole chapters 10/20/30, found [{string.Join(",", s1CustomChapters)}].");
            var s2CustomChapters = scenes.Where(s => s.season == 2 && s.animation == SceneAnimation.Custom)
                .Select(s => s.chapter).Distinct().OrderBy(c => c).ToArray();
            CollectionAssert.AreEqual(new[] { 10, 15, 21, 30 }, s2CustomChapters,
                $"Season Two Custom animation must sit only in tentpole chapters 10/15/21/30, found [{string.Join(",", s2CustomChapters)}].");
        }
    }
}
