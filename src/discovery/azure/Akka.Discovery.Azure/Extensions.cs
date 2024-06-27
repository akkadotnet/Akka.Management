using System.Linq;
using Akka.Configuration.Hocon;
using Akka.Util.Internal;

namespace Akka.Discovery.Azure;

public static class Extensions
{
    internal static Configuration.Config MoveTo(this Configuration.Config config, string path)
    {
        var rootObj = new HoconObject();
        var rootValue = new HoconValue();
        rootValue.Values.Add(rootObj);
            
        var lastObject = rootObj;

        var keys = path.SplitDottedPathHonouringQuotes().ToArray();
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];
            var innerObject = new HoconObject();
            var innerValue = new HoconValue();
            innerValue.Values.Add(innerObject);
                
            lastObject.GetOrCreateKey(key);
            lastObject.Items[key] = innerValue;
            lastObject = innerObject;
        }
        lastObject.Items[keys[keys.Length - 1]] = config.Root;
            
        return new Configuration.Config(new HoconRoot(rootValue));
    }
}