using Unity.Entities;

namespace E7.EcsTesting
{
    /// <summary>
    /// Performs higher level, one-off operation on <see cref="EntityManager"/>.
    /// Many methods allocate and immediately dispose <see cref="EntityQuery"/> inside each call.
    ///
    /// This system independent shortcuts are useful for unit testing so you can query and check in one line.
    ///
    /// There is no `CompleteAllJobs` inside. There is a chance that it would cause
    /// error when someone else is reading/writing the same data.
    /// </summary>
    public partial class EntityAssertionQuery
    {
        EntityManager em;
        public EntityAssertionQuery(World world)
        {
            this.em = world.EntityManager;
        }
    }
}