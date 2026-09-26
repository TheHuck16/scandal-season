// UNITY-DEPENDENT — PlayMode smoke tests. Run on device/player via
// Window > General > Test Runner, and in CI via game-ci/unity-test-runner.
// They prove the thin Unity views initialize their domain objects with no
// content loaded. Game logic itself is covered by the .NET domain suite.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ScandalSeason.Tests.PlayMode
{
    public sealed class ViewSmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator Views_InitializeWithNoContent()
        {
            var go = new GameObject("ScandalSeasonSmoke");
            var board = go.AddComponent<MergeBoardView>();
            var economy = go.AddComponent<EconomyView>();
            var scoring = go.AddComponent<ScoringView>();
            var progression = go.AddComponent<ProgressionView>();
            yield return null;

            Assert.IsNotNull(board.Board, "MergeBoardView must construct its domain board in Awake.");
            Assert.IsNotNull(economy.Wallet, "EconomyView must construct its wallet in Awake.");
            Assert.IsNotNull(economy.Energy, "EconomyView must construct its energy system in Awake.");
            Assert.IsNotNull(progression.Progression, "ProgressionView must construct its progression in Awake.");
            Assert.IsNotNull(scoring, "ScoringView must attach cleanly.");

            progression.AdvanceSeason();
            Assert.AreEqual(2, progression.Progression.CurrentSeasonNumber);

            Object.Destroy(go);
        }
    }
}
