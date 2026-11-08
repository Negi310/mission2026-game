using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace TankTown.Tests
{
    public sealed class TankTownWheelMenuTests
    {
        [UnityTest]
        public IEnumerator SteeringHasNeutralGateAndConfirmInvokesOnlyOneChoice()
        {
            TankTownGame.DisableRecordSavingForTests = true;
            SceneManager.LoadScene("TankTown"); yield return null;
            var game = Object.FindFirstObjectByType<TankTownGame>();
            game.Input.useHardware = true; game.Input.SetWheel(1); yield return null;
            var canvas = new GameObject("Test carousel canvas", typeof(Canvas));
            var style = new ToyHudStyle(); var font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
            var carousel = new TankTownCardCarousel(game, canvas.transform, font, style, "Test carousel");
            int first = 0, second = 0;
            carousel.Set("Test", "", new[] {
                new TankTownCardCarousel.Choice("A", "", "GO", Color.red, () => first++),
                new TankTownCardCarousel.Choice("B", "", "GO", Color.blue, () => second++) });
            carousel.Tick(.1f); Assert.That(carousel.Selected, Is.Zero, "Held wheel on entry requires neutral first");
            game.Input.SetWheel(0); yield return null; carousel.Tick(.1f);
            game.Input.SetWheel(.3f); yield return null; carousel.Tick(.1f); Assert.That(carousel.Selected, Is.Zero, "Small noise cannot change selection");
            game.Input.SetWheel(1); yield return null; carousel.Tick(.1f); Assert.That(carousel.Selected, Is.EqualTo(1));
            carousel.Confirm(); carousel.Confirm(); Assert.That(second, Is.EqualTo(1)); Assert.That(first, Is.Zero);
            Object.Destroy(canvas); style.Dispose(); game.Input.SetWheel(0);
            TankTownGame.DisableRecordSavingForTests = false;
        }
    }
}
