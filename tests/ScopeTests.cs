﻿#if !NETCOREAPP1_0

namespace Dawn.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class ScopeTests : BaseTests
    {
        [Fact(DisplayName = T + "BeginScope")]
        public void BeginScope()
        {
            var validation =
                (from m in typeof(Guard).GetMethods(BindingFlags.Public | BindingFlags.Static)
                 where m.Name == nameof(Guard.NotNull)
                 let g = m.GetGenericArguments()
                 where g.Length == 1 && g[0].GetGenericParameterConstraints().Length == 0
                 let p = m.GetParameters()
                 where p.Length == 2 && p[1].ParameterType == typeof(string)
                 select m).Single();

            Task.WaitAll(
                Task.Run(Async()),
                Task.Run(Async()));

            Func<Task> Async() => async () =>
            {
                // Outer
                var id = 0;
                Exception outerIntercepted = null;
                Guard.BeginScope((ex, stackTrace) =>
                {
                    Assert.Same(stackTrace.GetFrame(0).GetMethod(), validation);
                    id++;
                    outerIntercepted = ex;
                });

                var lastId = id;
                for (var i = 0; i < 5; i++)
                {

                    Test(ref outerIntercepted);
                    Assert.Equal(lastId + 1, id);
                    id--;

                    await Delay();

                    Test(ref outerIntercepted);
                    Assert.Equal(lastId + 1, id);
                    id--;

                    await Delay().ConfigureAwait(false);

                    Test(ref outerIntercepted);
                    Assert.Equal(lastId + 1, id);
                    id--;
                }

                // Inner with propagation
                id = 0;
                lastId = id;
                for (var i = 0; i < 5; i++)
                {
                    if (i == 3)
                        Guard.BeginScope(null); // Should have no effect.

                    Exception innerIntercepted = null;
                    using (Guard.BeginScope((ex, stackTrace) =>
                    {
                        Assert.Same(stackTrace.GetFrame(0).GetMethod(), validation);
                        id++;
                        innerIntercepted = ex;
                    }))
                    {
                        Test(ref innerIntercepted);
                        Assert.Equal(lastId + 2, id);
                        id -= 2;

                        await Delay();

                        Test(ref innerIntercepted);
                        Assert.Equal(lastId + 2, id);
                        id -= 2;

                        await Delay().ConfigureAwait(false);

                        Test(ref innerIntercepted);
                        Assert.Equal(lastId + 2, id);
                        id -= 2;
                    }
                }

                // Inner without propagation
                id = 0;
                lastId = id;
                for (var i = 0; i < 5; i++)
                {
                    if (i == 3)
                        Guard.BeginScope(null, false); // Should stop propagation.

                    Exception innerIntercepted = null;
                    using (Guard.BeginScope((ex, stackTrace) =>
                    {
                        Assert.Same(stackTrace.GetFrame(0).GetMethod(), validation);
                        id += 3;
                        innerIntercepted = ex;
                    }, i >= 3))
                    {
                        Test(ref innerIntercepted);
                        Assert.Equal(lastId + 3, id);
                        id -= 3;

                        await Delay();

                        Test(ref innerIntercepted);
                        Assert.Equal(lastId + 3, id);
                        id -= 3;

                        await Delay().ConfigureAwait(false);

                        Test(ref innerIntercepted);
                        Assert.Equal(lastId + 3, id);
                        id -= 3;
                    }
                }
            };

            void Test(ref Exception intercepted)
            {
                try
                {
                    Guard.Argument(null as string, "value").NotNull();
                }
                catch (ArgumentNullException ex)
                {
                    Assert.Same(ex, intercepted);
                }
            }

            Task Delay()
                => Task.Delay(RandomUtils.Current.Next(10, 20));
        }
    }
}

#endif
