using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class StarterPlayTests
{
    [UnityTest]
    public IEnumerator PlayerLoopAdvancesFrames()
    {
        var frame = Time.frameCount;
        yield return null;
        yield return null;
        Assert.That(Time.frameCount, Is.GreaterThan(frame));
    }
}
