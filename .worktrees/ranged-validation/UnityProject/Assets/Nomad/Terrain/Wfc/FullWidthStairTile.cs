using UnityEngine;

namespace Nomad.Terrain
{
    public enum FullWidthStairLayout { U, L }

    public sealed class FullWidthStairTile : MonoBehaviour
    {
        [SerializeField] private FullWidthStairLayout layout;
        [SerializeField] private bool inverted;
        public FullWidthStairLayout Layout => layout;
        public bool Inverted => inverted;

        public void Configure(FullWidthStairLayout shape, bool isInverted)
        {
            layout = shape;
            inverted = isInverted;
        }
    }
}
