using System.Linq;
using NevrianGlowFarm.Logic;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class LogicAssemblyTests
    {
        [Test]
        public void LogicAssembly_DoesNotReferenceUnityEngine()
        {
            var assembly = typeof(LogicAssembly).Assembly;

            var unityRefs = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"))
                .ToArray();

            Assert.AreEqual(LogicAssembly.Name, assembly.GetName().Name);
            Assert.IsEmpty(unityRefs, "Logic 어셈블리는 엔진에 의존하면 안 된다: " + string.Join(", ", unityRefs));
        }
    }
}
