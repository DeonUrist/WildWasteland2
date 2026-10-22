using UnityEngine;

// Survival & environment contracts (core-modapi.md 8): weather regions, storm queries, shelters, day light, weather visual policy.
// Provider: Survival (IWeatherVisualPolicy: Core Optimization). Consumers: NPCAI, Vehicles, HUD, Compatibility. FROZEN after W0.
namespace WildWasteland.Core
{
    // ------------------------------------------------------------------ moved unchanged from Services.cs

    /// <summary>Weather render detail chosen by the performance preset.</summary>
    public enum WeatherVisualDetail { Low, Balanced, Max }

    /// <summary>Weather render detail policy (provider: Core Optimization).</summary>
    public interface IWeatherVisualPolicy
    {
        /// <summary>Current render detail.</summary>
        WeatherVisualDetail WeatherDetail { get; }
    }

    /// <summary>One weather region (storm front or hurricane), world space.</summary>
    public struct WeatherRegion
    {
        /// <summary>Centre, world space.</summary>
        public Vector3 WorldCenter;
        /// <summary>Half extents (x across, y along), metres.</summary>
        public Vector2 Radii;
        /// <summary>Velocity, m/s.</summary>
        public Vector3 Velocity;
        /// <summary>Heading, degrees.</summary>
        public float Heading;
        /// <summary>Hurricane (rotating column) instead of a front.</summary>
        public bool Hurricane;
        /// <summary>Area of the boundary polygon, km^2.</summary>
        public float AreaSquareKm
        {
            get
            {
                float area = 0; Vector2 a = Boundary(63);
                for (int i = 0; i < 64; i++) { Vector2 b = Boundary(i); area += a.x*b.y-b.x*a.y; a = b; }
                return Mathf.Abs(area)*.0000005f;
            }
        }
        /// <summary>Edge wave offset along the front at normalized x.</summary>
        public static float EdgeWave(float x) { return .055f*Mathf.Sin(x*3.7f+.4f)+.025f*Mathf.Sin(x*10.3f+.9f); }
        /// <summary>Normalized distance to the region edge (1 = centre, 0 = edge, negative = outside).</summary>
        public static float EdgeDistance(float x, float z)
        {
            z -= EdgeWave(x);
            float u = Mathf.Abs(x)/Mathf.Max(.1f,.875f-.125f*z), v = Mathf.Abs(z);
            float u2 = u*u, v2 = v*v;
            return 1-Mathf.Pow(u2*u2*u2+v2*v2*v2,1f/6);
        }
        /// <summary>Boundary point <paramref name="point"/> of 64, local metres.</summary>
        public Vector2 Boundary(int point)
        {
            float angle = point*Mathf.PI*2/64, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float z = Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),1f/3);
            float x = Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),1f/3)*(.875f-.125f*z);
            return new Vector2(x*Radii.x,(z+EdgeWave(x))*Radii.y);
        }
    }

    /// <summary>Weather regions and generation tuning. Storm queries live in <see cref="IStormQueries"/>.</summary>
    public interface IWeatherRegions
    {
        /// <summary>Number of regions.</summary>
        int Count { get; }
        /// <summary>Show regions on the map.</summary>
        bool MapAreas { get; set; }
        /// <summary>Hurricane density.</summary>
        float HurricaneDensity { get; set; }
        /// <summary>Front width, km.</summary>
        float FrontWidthKm { get; set; }
        /// <summary>Front depth, km.</summary>
        float DepthKm { get; set; }
        /// <summary>Region by index.</summary>
        bool TryGet(int index, out WeatherRegion region);
    }

    // ------------------------------------------------------------------ new (Wave B Survival)

    /// <summary>Storm identity and exposure (README 6.8): replaces NPC Hazards/Storm's own native-storm reading.</summary>
    public interface IStormQueries
    {
        /// <summary>Storm exposure 0..1 at a world point (0 = clear, 1 = inside a critical storm).</summary>
        float StormExposure(Vector3 world);
        /// <summary>True for the root of a native/replaced storm object (prefab id 5697600806714206537).</summary>
        bool IsStormRoot(GameObject go);
        /// <summary>Number of active storms.</summary>
        int ActiveStorms { get; }
        /// <summary>True when any storm is within <paramref name="radius"/> of a world point.</summary>
        bool AnyNear(Vector3 world, float radius);
    }

    /// <summary>Provider of shelter for actors (War Rig trailers, buildings).</summary>
    public interface IShelterProvider
    {
        /// <summary>True when the actor at the world point is sheltered by this provider.</summary>
        bool Shelters(GameObject actor, Vector3 world);
    }

    /// <summary>Shelter registry consulted by storm hazards (replaces the implicit War Rig shelter via Explosion.ShouldApplyForce).</summary>
    public interface IShelterRegistry
    {
        /// <summary>Registers a provider; returns an id.</summary>
        int Register(IModule owner, IShelterProvider provider);
        /// <summary>Removes a provider.</summary>
        void Remove(int id);
        /// <summary>True when any provider shelters the actor.</summary>
        bool IsSheltered(GameObject actor);
        /// <summary>True when any provider shelters a world point (no actor).</summary>
        bool IsSheltered(Vector3 world);
    }

    /// <summary>Day light query (ex Patrols DayLight).</summary>
    public interface IDayLight
    {
        /// <summary>Ambient light level 0 (night) .. 1 (noon).</summary>
        float Level { get; }
        /// <summary>True at night.</summary>
        bool Night { get; }
        /// <summary>Light level at a world point (storms/shade darken it).</summary>
        float At(Vector3 world);
    }
}
