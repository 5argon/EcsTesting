using Unity.Entities;

namespace E7.EcsTesting
{
    public abstract class EcsTestBase
    {
        protected World w { get; private set; }
        protected EntityManager em { get; private set; }
        protected EntityAssertionQuery eaq { get; private set; }

        protected void SetUpBase()
        {
            w = new World("Test World");
            em = w.EntityManager;
            eaq = new EntityAssertionQuery(w);
        }

        /// <summary>
        /// Call to make the next world update go in a specific time.
        /// </summary>
        protected void ForceDeltaTime(float deltaTime)
        {
            w.GetExistingSystem<ConstantDeltaTimeSystem>().ForceDeltaTime(deltaTime);
        }

        protected void TearDownBase()
        {
            w.Dispose();
        }
    }
}