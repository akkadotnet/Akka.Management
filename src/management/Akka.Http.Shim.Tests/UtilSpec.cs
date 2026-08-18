// -----------------------------------------------------------------------
//  <copyright file="UtilSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using Akka.Http.Extensions;
using Xunit;

namespace Akka.Http.Shim.Tests;

public class UtilSpec
{
    [Fact(DisplayName = "Shuffle should not drop or alter values")]
    public void ShuffleTest()
    {
        var list = Enumerable.Range(0, 100).ToList();
        list.Shuffle();
        Assert.Equal(100, list.Count);
        foreach (var i in Enumerable.Range(0, 100))
        {
            Assert.Contains(i, list);
        }
    }
}