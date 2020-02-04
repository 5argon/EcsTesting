using System;
using System.Collections.Generic;
using Unity.Collections;
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

        public int EntityCountComponentObject<CD>(Func<CD, bool> where = null) where CD : class, IComponentData
        {
            return ComponentObjects<CD>(where).Length;
        }

        public CD[] ComponentObjects<CD>(Func<CD, bool> where = null) where CD : class, IComponentData
        {
            CD[] array;
            using (var eq = em.CreateEntityQuery(
                ComponentType.ReadOnly<CD>()
            ))
            {
                var na = eq.ToEntityArray(Allocator.TempJob);
                array = new CD[na.Length];
                for (int i = 0; i < na.Length; i++)
                {
                    array[i] = em.GetComponentObject<CD>(na[i]);
                }
            }

            if (where == null)
            {
                return array;
            }

            List<CD> list = new List<CD>();
            for (int i = 0; i < array.Length; i++)
            {
                if (where.Invoke(array[i]) == true)
                {
                    list.Add(array[i]);
                }
            }

            return list.ToArray();
        }

        public CD GetSingleObject<CD>() where CD : class, IComponentData
        {
            return em.GetComponentObject<CD>(GetSingleEntityObject<CD>());
        }

        public Entity GetSingleEntityObject<CD>() where CD : class, IComponentData
        {
            using (var eq = em.CreateEntityQuery(
                ComponentType.ReadOnly<CD>()
            ))
            {
                var na = eq.ToEntityArray(Allocator.TempJob);

                var array = na.ToArray();
                na.Dispose();
                if (array.Length != 1)
                {
                    throw new System.InvalidOperationException(
                        $"GetSingleObject() requires that exactly one exists but there are {array.Length}.");
                }

                return array[0];
            }
        }
    }
}