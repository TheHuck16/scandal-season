// UNITY-DEPENDENT — EditMode tests. Run inside the Unity Editor via
// Window > General > Test Runner, and in CI via game-ci/unity-test-runner.
// They assert the same locked rules the .NET suite covers, compiled against
// the real ScandalSeason.Domain assembly as Unity builds it.

using NUnit.Framework;
using ScandalSeason.Domain.Economy;
using ScandalSeason.Domain.Progression;

namespace ScandalSeason.Tests.EditMode
{
    public sealed class LockedRulesEditModeTests
    {
        [Test]
        public void NoAdsOfAnyKind() =>
            Assert.IsFalse(GameRules.AdsEnabled, "Revenue is IAP only. Not even rewarded opt-in.");

        [Test]
        public void CurrenciesAreCrownsAndCoins()
        {
            Assert.AreEqual("Crowns", GameRules.PremiumCurrencyName);
            Assert.AreEqual("Coins", GameRules.SoftCurrencyName);
        }

        [Test]
        public void FairnessLine() =>
            Assert.AreEqual("No ads. Time earns everything.", GameRules.FairnessLine);

        [Test]
        public void BookOneIsTenSeasons_SeasonsContinueAfter()
        {
            Assert.AreEqual(10, GameRules.BookOneSeasons);
            var progression = new SeasonProgression();
            for (int i = 0; i < 25; i++)
                progression.AdvanceSeason(); // well past Book One — never throws
            Assert.IsTrue(progression.IsBookOneComplete);
            Assert.AreEqual(26, progression.CurrentSeasonNumber);
        }

        [Test]
        public void SeasonStructureIs30x40()
        {
            Assert.AreEqual(30, GameRules.ChaptersPerSeason);
            Assert.AreEqual(40, GameRules.ScenesPerChapter);
        }

        [Test]
        public void EventPassWindowIs3To14Days()
        {
            Assert.AreEqual(3, GameRules.MinEventPassDays);
            Assert.AreEqual(14, GameRules.MaxEventPassDays);
        }
    }
}
