using NUnit.Framework;
using Unity.Entities;

namespace E7.EcsTesting
{
    /// <summary>
    /// Base class to begin writing a world-based test. It pours all systems available in to the world, plus
    /// one more system which allows you to <see cref="EcsTestBase.ForceDeltaTime"/> to change delta time of the next update.
    /// 
    /// You then create some entities and run `w.Update()` in sequences and check results.
    /// </summary>
    public abstract class WorldTestBase : EcsTestBase
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
            var allSystems =
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default, requireExecuteAlways: false);
            allSystems.Add(typeof(ConstantDeltaTimeSystem)); //Need to be added with UpdateWorldTimeSystem at the same time.
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(w, allSystems);
        }
    }
}