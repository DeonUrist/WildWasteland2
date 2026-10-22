namespace WildWasteland.Core
{
    /// <summary>Presentation-owned cargo texture choices used by authoring menus.</summary>
    public interface ICargoArtwork { string[] Textures(); string File(string key); }
}
