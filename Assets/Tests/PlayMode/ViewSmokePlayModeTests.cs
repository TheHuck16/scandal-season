// UNITY-DEPENDENT — PlayMode smoke tests. Run on device/player via
// Window > General > Test Runner, and in CI via game-ci/unity-test-runner.
// They prove the game layer initializes its domain objects with no content
// loaded. Game logic itself is covered by the .NET domain suite.

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ScandalSeason.Tests.PlayMode
{
    public sealed class ViewSmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameManager_InitializesWithNoContent()
        {
            var go = new GameObject("ScandalSeasonSmoke");
            var game = go.AddComponent<GameManager>();
            yield return null;

            game.InitializeSession(
                new List<SceneDefinitionSO>(),
                new List<SceneDefinitionSO>(),
                new List<SceneDefinitionSO>(),
                new List<ItemChainDefinitionSO>());

            Assert.IsNotNull(game.Wallet, "GameManager must construct its wallet.");
            Assert.IsNotNull(game.Energy, "GameManager must construct its energy system.");
            Assert.IsNotNull(game.Board, "GameManager must construct its merge board.");
            Assert.IsNotNull(game.Orders, "GameManager must construct its order queue.");
            Assert.IsNotNull(game.Progression, "GameManager must construct its progression.");
            Assert.AreEqual(GameState.Title, game.CurrentState);

            game.Progression.AdvanceSeason();
            Assert.AreEqual(2, game.Progression.CurrentSeasonNumber);

            Object.Destroy(go);
        }
    }
}
