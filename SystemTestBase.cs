using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;

namespace E7.EcsTesting
{
    /// <summary>
    /// Base class to begin writing a per-system unit test.
    /// You get a world containing a single system specified on <typeparam name="T"></typeparam>.
    /// You then create some entities and run `w.Update()` in sequences and check results.
    /// 
    /// You should update the world even though you only want to update the system,
    /// because there is one more system added
    /// which allows you to <see cref="EcsTestBase.ForceDeltaTime"/> to change delta time of the next update.
    /// It allows you to unit test a system that includes `Time` in its design.
    /// </summary>
    public abstract class SystemTestBase<T> : EcsTestBase where T : ComponentSystemBase
    {
        [SetUp]
        public void SetUp()
        {
            SetUpBase();
            SetUpWorld();
        }

        [TearDown]
        public void TearDown() => TearDownBase();

        private void SetUpWorld()
        {
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(w,
                new List<Type> {typeof(T), typeof(ConstantDeltaTimeSystem)});
        }
    }
}