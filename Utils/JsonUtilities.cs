using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TechnoartSDK.Utils;

public static class JsonUtilities
{
    public static JsonElement GetJsonSchema<T,K>(List<string> ExcludeProps)
    {
        var js = new JsonSerializerOptions()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                    {
                        typeInfo =>
                        {
                            if (typeInfo.Type != typeof(K)) return;

                            foreach (var propName in ExcludeProps)
                            {
                                typeInfo.Properties.Remove(
                                    typeInfo.Properties.First(p=>
                                    p.Name.Equals(
                                        propName,
                                        StringComparison.InvariantCultureIgnoreCase)));
                            }
                        }
                    }
            }
        };
        var res = AIJsonUtilities.CreateJsonSchema(
            typeof(T),
            serializerOptions: js);
        return res;
    }
}
