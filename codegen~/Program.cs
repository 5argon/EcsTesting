using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace codegen
{
    class Program
    {
        static int[] tag = new int[] { 0, 1, 2, 3, 4, 5, 6 };
        static int[] scd = new int[] { 0, 1, 2 };
        static StringWriter sw = new StringWriter();

        static void Main(string[] args)
        {
            for (int i = 0; i < tag.Length; i++)
            {
                for (int j = 0; j < scd.Length; j++)
                {
                    Gen1(tag[i], scd[j]);
                }
            }
            File.WriteAllText("./EntityManagerUtilitySingleton.gen", sw.ToString());
        }

        const string gsGetSingleton = @"
/// <summary>
/// Like `GetSingleton` in system but usable from outside.
/// You can add upto 0~6 CD and 0~2 SCD types to the query.
/// The first type is always the returning value.
/// </summary>
public <<MAIN>> GetSingleton<<NOFILTER>><<<TYPEHOR>>>(<<ARGS>>)
<<WHERE>>
{
    using (var eq = em.CreateEntityQuery(
        <<TYPEVERT>>
    ))
    {
        <<FILTER>>
        return eq.GetSingleton<<<MAIN>>>();
    }
}
";

        const string gsGetSingletonScdFirst = @"
/// <summary>
/// Like `GetSingleton` in system but usable from outside.
/// You can add upto 0~6 CD and 0~2 SCD types to the query.
/// The first type is always the returning value.
/// </summary>
public <<MAIN>> GetSingleton<<NOFILTER>><<<TYPEHOR>>>(<<ARGS>>)
<<WHERE>>
{
    using (var eq = em.CreateEntityQuery(
        <<TYPEVERT>>
    ))
    {
        <<FILTER>>
        return em.GetSharedComponentData<<<MAIN>>>(eq.GetSingletonEntity());
    }
}
";


        const string gsGetSingletonEntity = @"
/// <summary>
/// Like `GetSingletonEntity` in system but usable from outside.
/// 
/// You can add upto 0~6 CD and 0~2 SCD types to the query and also where filter
/// based on any of the CD. Query, SCD filter, and where filter combined must
/// produce 1 entity. Automatically throws if it wasn't, which is useful in tests.
/// </summary>
public Entity GetSingletonEntity<<NOFILTER>><<<TYPEHOR>>>(<<ARGS>>)
<<WHERE>>
{
    var ea = Entities<<<TYPEHOR>>>(<<ARGSFORWARD>>);
    if (ea.Length != 1)
    {
        throw new System.InvalidOperationException($""GetSingletonEntity() requires that exactly one exists but there are { ea.Length }."");
    }
    return ea[0];
}
";

        const string gsEntityCount = @"
/// <summary>
/// Count entities that are returned from All query made of all components on generic type arguments.
/// You can add upto 0~6 CD and 0~2 SCD types to the query.
/// </summary>
/// <remarks>
/// In the argument :
/// 
/// - Add a lambda with input argument typed the same as component data specified
/// in the generic type argument. This is a filter to only work on an entity that
/// pass the criteria. (Like LINQ's `.Where`) It is possible to specify just a subset
/// of all component data in the generic type as long as the omitted types come later
/// when counting from left to right.
/// 
/// - Add from 0 to 2 SCD value filter, if you have enough SCD generic type specified.
/// Use `nf: false` in place of actual value to skip filtering that SCD type.
/// With that it is possible to use SCD types as one of tag components.
/// </remarks>
public int EntityCount<<NOFILTER>><<<TYPEHOR>>>(<<ARGS>>)
<<WHERE>>
{
    using (var eq = em.CreateEntityQuery(
        <<TYPEVERT>>
    ))
    {
        <<FILTER>>
        var na = eq.ToEntityArray(Allocator.TempJob);
        <<WHEREABLES>>
        int count = na.Length;
        na.Dispose();
        return count;
    }
}
";

        const string gsGet = @"
/// <summary>
/// Return a linearized component data array of the first component of generic type arguments.
/// You can add additional components upto 6 CD and upto 2 SCD types to the query.
/// </summary>
public <<MAIN>>[] Components<<NOFILTER>><<<TYPEHOR>>>(<<ARGS>>)
<<WHERE>>
{
    using (var eq = em.CreateEntityQuery(
        <<TYPEVERT>>
    ))
    {
        <<FILTER>>
        var na = eq.ToComponentDataArray<<<MAIN>>>(Allocator.TempJob);
        var array = na.ToArray();
        na.Dispose();
        return array;
    }
}
";
        const string gsEntities = @"
/// <summary>
/// Return a linearized entity array from All query made of all components on generic type arguments.
/// You can add upto 0~6 CD and 0~2 SCD types to the query.
/// </summary>
/// <remarks>
/// In the argument :
/// 
/// - Add a lambda with input argument typed the same as component data specified
/// in the generic type argument. This is a filter to only work on an entity that
/// pass the criteria. (Like LINQ's `.Where`) It is possible to specify just a subset
/// of all component data in the generic type as long as the omitted types come later
/// when counting from left to right.
/// 
/// - Add from 0 to 2 SCD value filter, if you have enough SCD generic type specified.
/// Use `nf: false` in place of actual value to skip filtering that SCD type.
/// With that it is possible to use SCD types as one of tag components.
/// </remarks>
public Entity[] Entities<<NOFILTER>><<<TYPEHOR>>>(<<ARGS>>)
<<WHERE>>
{
    using (var eq = em.CreateEntityQuery(
        <<TYPEVERT>>
    ))
    {
        <<FILTER>>
        var na = eq.ToEntityArray(Allocator.TempJob);
        <<WHEREABLES>>
        var array = na.ToArray();
        na.Dispose();
        return array;
    }
}
";

        const string whereableCheck = @"
NativeList<Entity> filtered = new NativeList<Entity>(na.Length, Allocator.Temp);
<<WHEREABLEUSINGS>>
{
    for (int i = 0; i < na.Length; i++)
    {
        if(where(<<WHEREABLEWHERE>>))
        {
            filtered.Add(na[i]);
        }
    }
}
na.Dispose();
na = new NativeArray<Entity>(filtered.Length, Allocator.Temp);
for(int i = 0;i<filtered.Length;i++)
{
    na[i] = filtered[i];
}
filtered.Dispose();
";

        static void Gen1(int tag, int scd)
        {
            if (tag == 0 && scd == 0) return;

            for (int whereable = 0; whereable <= tag; whereable++)
            {
                //Every time there is an scd, gen a no filter, and filtered (1,2) version.
                for (int ft = 0; ft < scd + 1; ft++)
                {
                    List<string> componentTypes = new List<string>();
                    List<string> whereableTypes = new List<string>();
                    List<string> whereableUsings = new List<string>();
                    List<string> whereableWhere = new List<string>();
                    List<string> scds = new List<string>();
                    List<string> args = new List<string>();
                    List<string> argsForward = new List<string>();
                    List<string> argsFilter = new List<string>();
                    for (int i = 0; i < tag; i++)
                    {
                        componentTypes.Add($"CD{i + 1}");
                        if (i < whereable)
                        {
                            whereableTypes.Add($"CD{i + 1}");
                            whereableUsings.Add($"using(var cd{i + 1}Cda = eq.ToComponentDataArray<CD{i + 1}>(Allocator.TempJob))");
                            whereableWhere.Add($"cd{i + 1}Cda[i]");
                        }
                    }
                    if (whereable != 0)
                    {
                        args.Add($"Func<{string.Join(",", whereableTypes)}, bool> where");
                        argsForward.Add("where");
                    }
                    for (int i = 0; i < scd; i++)
                    {
                        componentTypes.Add($"SCD{i + 1}");
                        if (i >= ft)
                        {
                            scds.Add($"filter{i + 1}");
                            args.Add($"SCD{i + 1} filter{i + 1}");
                            argsForward.Add($"filter{i + 1}");
                        }
                        else
                        {
                            args.Add($"bool nf{(scd > 1 ? (i + 1).ToString() : string.Empty)}");
                            argsForward.Add($"nf{(scd > 1 ? (i + 1).ToString() : string.Empty)}");
                        }
                    }
                    string typeHor = string.Join(",", componentTypes);
                    string argsString = scd > 0 || whereable > 0 ? string.Join(",", args) : string.Empty;
                    string wheres = string.Join("\n", componentTypes.Select(x =>
                    {
                        if (x.Contains("SCD"))
                        {
                            return $"where {x} : struct, ISharedComponentData";
                        }
                        else
                        {
                            return $"where {x} : struct, IComponentData";
                        }
                    }));
                    string typeVert = string.Join(",\n", componentTypes.Select(x => $"ComponentType.ReadOnly<{x}>()"));
                    string filters = scd > 0 && ft != scd ?
                    $"eq.SetSharedComponentFilter({string.Join(",", scds)});"
                    : string.Empty;

                    if (whereable == 0)
                    {
                        if (tag != 0)
                        {
                            Do(gsGetSingleton);
                        }
                        else
                        {
                            Do(gsGetSingletonScdFirst);
                        }

                        if (tag != 0)
                        {
                            Do(gsGet);
                        }
                    }

                    Do(gsGetSingletonEntity);
                    Do(gsEntityCount);
                    Do(gsEntities);

                    void Do(string tem)
                    {
                        sw.WriteLine(tem
                            .Replace("<<MAIN>>", componentTypes[0])
                            .Replace("<<NOFILTER>>", string.Empty)
                            .Replace("<<TYPEHOR>>", typeHor)
                            .Replace("<<ARGS>>", argsString)
                            .Replace("<<ARGSFORWARD>>", string.Join(",",argsForward))
                            .Replace("<<WHERE>>", wheres)
                            .Replace("<<TYPEVERT>>", typeVert)
                            .Replace("<<FILTER>>", filters)
                            .Replace("<<WHEREABLES>>", whereable == 0 ? string.Empty : whereableCheck.Replace("<<WHEREABLEUSINGS>>", string.Join("\n", whereableUsings)).Replace("<<WHEREABLEWHERE>>", string.Join(",", whereableWhere)))
                        );
                    }
                }
            }
        }
    }
}
