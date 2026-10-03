using UnityEngine;

namespace TankTown
{
    public sealed class TownTrafficSignal : MonoBehaviour
    {
        public Vector3 Intersection { get; private set; }
        public bool NorthSouthOpen => Phase < 5;
        private float Phase => Mathf.Repeat(Time.time + offset, 12);
        private float offset;
        private TankTownGame game;
        private DestructibleBlock block;
        private Renderer[] bulbs;
        private MaterialPropertyBlock tint;
        private readonly Color[] colors = { new Color(1, .08f, .03f), new Color(1, .83f, .01f), new Color(.1f, 1, .4f) };

        public void Initialize(TankTownGame owner, Vector3 junction, Renderer[] lights, float phase)
        {
            game = owner; Intersection = junction; bulbs = lights; offset = phase;
            block = GetComponent<DestructibleBlock>(); tint = new MaterialPropertyBlock();
        }

        public bool ShouldStop(Vector3 position, Vector3 heading)
        {
            if (block.Detached) return false;
            bool northSouth = Mathf.Abs(heading.z) > Mathf.Abs(heading.x);
            bool open = northSouth ? Phase < 5 : Phase >= 6 && Phase < 11;
            if (open) return false;
            var delta = Vector3.ProjectOnPlane(Intersection - position, Vector3.up);
            float ahead = Vector3.Dot(delta, heading);
            return ahead > 3 && ahead < 9 && Mathf.Abs(Vector3.Dot(delta, Vector3.Cross(Vector3.up, heading))) < game.World.RoadWidth * .5f;
        }

        private void Update()
        {
            if (block.Detached) return;
            for (int i = 0; i < bulbs.Length; i++)
            {
                int active = i < 3 ? Phase < 5 ? 2 : Phase < 6 ? 1 : 0 : Phase >= 6 && Phase < 11 ? 2 : Phase >= 11 ? 1 : 0;
                int lamp = i % 3;
                tint.SetColor("_BaseColor", colors[lamp] * (lamp == active ? 1 : .16f));
                bulbs[i].SetPropertyBlock(tint);
            }
        }
    }
}
