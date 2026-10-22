using WildWasteland.Core;
namespace WildWasteland.Modules.Presentation
{
    /// <summary>Exposes real installed cargo artwork metadata without exposing paint implementation.</summary>
    internal sealed class CargoArtwork : ICargoArtwork
    {
        public string[] Textures() { return Paint.CargoTextures(); }
        public string File(string key) { return Paint.CargoFile(key); }
    }
}
