# ECS Testing Package

# `EntityAssertionQuery`

Helper object to query `EntityManager`'s content for assertion without care about performance. Initialize the instance by giving it your `World` then its various instance method can query entity content for you in a **single line**, combining `EntityQuery` creation, query with filters, and disposing everything before returning you the value.

After getting your component or `Entity` for use with regular `EntityManager`, you then assert with regular NUnit methods. This just help you get the things you want to assert, not for assertion.

## Assertion problems

Whether you test by updating a system or updating a world, you want to check the current state of entities in the world at the end. More often using the same or very similar `EntityQuery` as used in the system. But those queries are tightly coupled in the system (e.g. from `GetEntityQuery`, which register reader/writer to that system), and trying to expose them out for purpose of testing is not a good idea either. It is better to assert separately from outsider standpoint.

`EntityManager` which you can get from your test world could do this via `CreateEntityQuery`, then you can construct query with `ComponentType` or `EntityQueryDesc` as you would in the system. In turn creating a code similar to query initialization ceremony in `OnCreate` of the system you are interested in. But still it is a lot of steps : 

- Create `EntityQueryDesc` or tons of `ComponentType.ReadOnly/Exclude` for `EntityManager.CreateEntityQuery`.
- Perform checks :
  - Amount assertion : Use `GetSingleton` or `GetSingletonEntity` if situation allows, or `CalculateEntityCount()` to roughly check without accessing component data.
  - Value assertion : Because you don't have `Entity` reference that your system worked on, it is either you use `ToComponentDataArray` or `ToEntityArray` and see all of them if they are all at expected value, or simply you want to know there *exist* an entity with that value. Asserting on `NativeArray<T>` returned with `[0]` `[1]` etc. creates more problem as ordering is not guaranteed. It may work now but break later if you decided to upgrade your system that it add/remove component (Especially with `EntityCommandBuffer` usage in jobs, where the playback would affect order of chunk movement.) Proper thing you should do is always *searching* because it makes ordering irrelevant, but it is very troublesome to write.
  - Often you also want to find an `Entity` that its component `A` is this value, but you want to assert on its other `B` component. To do this you must do both `ToComponentDataArray<A>` and `ToEntityArray`, linear search on `NativeArray<A>`, then use the index to get `Entity` from entity array, then finally use `EntityManager.GetComponentData<B>` with that `Entity`.
- Dispose all `NativeArray` involved and also the `EntityQuery`. You can use `using` block but it adds noise to the test code.

Doing this properly creates an unreadable test code and discourage you from throughly test the data. I have made `EntityAssertionQuery` to solve this.

## Methods

These are all available methods for use : 

### Returns component data

- `GetSingleton` : `EntityQuery.GetSingleton` equivalent.
- `Components` : `EntityQuery.ToComponentDataArray` equivalent but return managed array that you don't have to dispose.

### Returns `Entity`

- `GetSingletonEntity` : `EntityQuery.GetSingletonEntity` equivalent.
- `EntityCount` : `EntityQuery.CalculateEntityCount` equivalent.
- `Entities` :  `EntityQuery.ToEntityArray` equivalent but return managed array that you don't have to dispose.

What's different about their equivalent is that you specify your query via generic type arguments. You can use up to 6 `IComponentData` and up to 2 `ISharedComponentData`. `IComponentData` always come first. There is no `IBufferComponentData` support.

## Basic usage

`eaq` is an instance of `EntityAssertionQuery`, list the type you want to narrow down your query for assertion in `<>` :

```csharp
// When using methods that returns component data, the first type is the return type.
// All others are tags to further filter the result.
eaq.GetSingleton<CD1, CD2>(); //returns CD1
eaq.Components<CD1, CD2, CD3>(); //returns CD1

// Methods that returns `Entity` you can order however you like.
eaq.GetSingletonEntity<CD1, CD2, CD3>();
eaq.EntityCount<CD1>();
eaq.Entities<CD1, CD2>();
```

## How to incorporate SCD type

Whenever you used 1 or 2 `ISharedComponentData` added to the end of your list of `IComponentData`, you can add a shared component value filter to the argument in that same line (It will be forwarded to `eq.SetSharedComponentFilter`.) to further filter not just by `ISharedComponentData` *type* but only chunks that have an SCD index of that *value*.

If you do not want to filter but still want to use that `ISharedComponentData` as to match the chunk with that type, specify `nf: true` in the place you would use a filter value for that `ISharedComponentData`.

It is not possible to add `ISharedComponentData` type without adding argument, as C# overload resolution cannot differentiate methods that the only difference is type constraint. A `bool` is used as a workaround for this. (So it doesn't matter if you type `nf:true` or `nf:false` or just `true`/`false`, it won't be used. Just that `nf` is readable as "no filter".)

```csharp
// With SCD and a value filter
eaq.Components<CD1, SCD1>(scd1Value);

// With SCD but do not want to filter, all values allowed as long as it is a chunk with this SCD type.
eaq.Components<CD1, SCD1>(nf: true);
eaq.EntityCount<CD1, CD2, SCD1>(nf: true);


// You can replace just one half with no-filter. Replace from left to right.
eaq.Entities<CD1, CD2, SCD1, SCD2>(scd1Value, scd2Value);
eaq.Entities<CD1, CD2, SCD1, SCD2>(nf1: true, scd2Value);
eaq.Entities<CD1, CD2, SCD1, SCD2>(nf1: true, nf2:true);
```

## How to perform WHERE filter on `IComponentData`

You can add WHERE filter on multiple `IComponentData` (in the same sense as LINQ's `Where`), not just `ISharedComponentData` value filter. This basically linearize the query out with SCD filter in effect (if any) first, then `for` loop iterate to collect the one that match to a new array and return it to you. This is a big mess that pollute the test if you do it yourself. It is useful as a simple existence check without care about entity order, or for grab a hold of `Entity` that you want to assert its other component with regular `EntityManager`.

You do this by adding a lambda function returning `bool` (`true` = include in the result) before any `ISharedComponentData` value filter in the argument. (You can use both) The lambda function can contain any number of `IComponentData` up to what you specified on type argument, always ordered from left to right. So put the one that you don't want to perform WHERE filter on the right. (Such as tag components where it has no value to WHERE filter anyways.)

It is only available on methods that returns `Entity`. (`GetSingletonEntity`, `EntityCount`, and `Entities`.)

```csharp
// Typing where: is optional, but it did make the test more readable.
eaq.Entities<CD1, CD2, CD3>( where: cd1 => cd1.value % 2 == 0 );

// You can add more up to total `IComponentData` you specified.
// It is then filtering different components of the same entity.
// You cannot do like (cd1, cd3) in the lambda, it must be from left to right as listed in generic type argument.
eaq.GetSingletonEntity<CD1, CD2, CD3, CD4>( where: (cd1, cd2) => cd1.value % 2 == 0 && cd1.value + cd2.value = 555; );

// It is possible to use WHERE CD filter together with SCD value filter, just make sure SCD filter comes later.
eaq.EntityCount<CD1, CD2, CD3, SCD1, SCD2>( where: cd1 => cd1.value % 2 == 0, scd1Value, nf1: true);
```

Minus comments, all features combined totalled to 18000 lines of generated code. It may trouble your auto complete engine a bit.

# `SystemTestBase<T>`

## How to use it

If you subclass from this and put your system class type in `<T>`, you will get a `protected World w` with a single system `T`. Updating the world with `w.Update()` is then like directly updating that system allowing you to unit test it, but a bit better.

Because this world actually has one more system `ConstantDeltaTimeSystem` which allows you to unit test a system that depends on `Time`. Calling `ForceDeltaTime` let you specify a new fixed `Time` that arrives to your single system in the next world update and beyond. This is why you must update a world even though you only want to update a single system.

## Reviews

https://gametorrahod.com/ecs-testing-review#system-testing

# `WorldTestBase`

## How to use it

If you subclass from this, you will get a `protected World w` with **all** systems instantiated like runtime, including Unity's built-in systems and standard `ComponentSystemGroup` hierarchy with all systems sorted into them.

You can write functional/integration test where you prepare entities and `w.Update()` a couple of times and check result. It is recommended to prepare an entity with minimum component that you know a single or couple of related systems would activate their `OnUpdate` like you are unit testing those systems. It maybe helpful to instead think that a unit is no longer a system, but a combination of data.

Like `SystemTestBase<T>`, this world also has one more system `ConstantDeltaTimeSystem` which allows you to test systems that depends on `Time`. Calling `ForceDeltaTime` let you specify a new fixed `Time` that arrives to all your systems in the next world update and beyond.

## Reviews

https://gametorrahod.com/ecs-testing-review#world-testing
