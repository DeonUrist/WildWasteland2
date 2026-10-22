using NUnit.Framework;
using UnityEngine;

public class StarterEditTests
{
    [Test]
    public void TransformPreservesAuthoredPosition()
    {
        var instance = new GameObject("Starter test");
        try
        {
            var position = new Vector3(2, 3, 5);
            instance.transform.position = position;
            Assert.That(instance.transform.position, Is.EqualTo(position));
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
