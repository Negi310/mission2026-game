using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TankTown.Tests
{
    public sealed class TankTownEntranceTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() { TankTownGame.SelectedStage = TownStage.Town; Time.timeScale = 1; yield return null; }

        [UnityTest]
        public IEnumerator EveryHouseHasOneStreetFacingEntranceThatFliesWithItsWall()
        {
            foreach (var stage in new[] { TownStage.Town, TownStage.BigCity, TownStage.CandyTown })
            {
                TankTownGame.SelectedStage = stage; SceneManager.LoadScene("TankTown"); yield return null;
                var game = Object.FindFirstObjectByType<TankTownGame>(); game.StartOrRestart(); game.Input.useHardware = true;
                game.Tank.enabled = false; game.Tank.GetComponent<Rigidbody>().isKinematic = true;
                foreach (var explosive in game.World.Explosives) explosive.gameObject.SetActive(false);
                var houses = game.World.Buildings.Where(x => x.Style != 3).ToArray();
                Assert.That(houses.Length, Is.EqualTo(stage == TownStage.Town ? 33 : 132));
                foreach (var house in houses)
                {
                    var doors = house.GetComponentsInChildren<Transform>().Where(x => x.name == "Building entrance").ToArray();
                    Assert.That(doors.Length, Is.EqualTo(1), stage + " / " + house.name);
                    var door = doors[0]; var wall = door.GetComponentInParent<DestructibleBlock>();
                    Assert.That(wall.Kind, Is.EqualTo(BuildingPart.Wall)); Assert.That(wall.Level, Is.Zero);
                    Assert.That(door.position.x, Is.EqualTo(house.transform.position.x).Within(.005f));
                    Assert.That(door.position.y, Is.EqualTo(house.transform.position.y).Within(.005f));
                    Assert.That(door.position.z, Is.LessThan(house.transform.position.z - house.Width / 2));
                    Assert.That(Vector3.Dot(door.forward, Vector3.back), Is.GreaterThan(.999f));
                    Assert.That(door.GetComponentsInChildren<Transform>().Count(x => x.name == "Door handle"), Is.EqualTo(2));
                    Assert.That(door.GetComponentsInChildren<Collider>().Any(x => x.enabled), Is.False, "Door visuals must not add invisible obstacles");
                }
                foreach (var house in houses.GroupBy(x => x.Style).Select(x => x.First()))
                {
                    var door = house.GetComponentsInChildren<Transform>().Single(x => x.name == "Building entrance");
                    var wall = door.GetComponentInParent<DestructibleBlock>(); int before = game.BrokenBlocks, total = game.TotalBlocks;
                    wall.Break(wall.transform.position, 0);
                    Assert.That(wall.DebrisBodies, Does.Contain(door.GetComponentInParent<Rigidbody>()));
                    Assert.That(door.GetComponentsInChildren<Transform>().Count(x => x.name == "Door handle"), Is.EqualTo(2));
                    Assert.That(game.BrokenBlocks, Is.EqualTo(before + 1)); Assert.That(game.TotalBlocks, Is.EqualTo(total));
                }
                if (stage == TownStage.CandyTown)
                    Assert.That(game.World.Buildings.Single(x => x.Style == 3).GetComponentsInChildren<Transform>().Any(x => x.name == "Building entrance"), Is.False, "The doughnut already has an open passage");
            }
            yield return null;
        }
    }
}
