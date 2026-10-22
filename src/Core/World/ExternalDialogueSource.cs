using UnityEngine;
namespace WildWasteland.Core
{
    /// <summary>Read-only live external dialogue content; native buttons retain their own click behavior.</summary>
    public interface IExternalDialogueSource { bool Talking {get;} object Session {get;} string Speaker {get;} int Revision {get;} RectTransform Panel {get;} RectTransform Chat {get;} RectTransform Choices {get;} void Close(); }
}
